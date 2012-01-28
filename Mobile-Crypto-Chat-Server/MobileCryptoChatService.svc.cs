using System;
using System.Data.SqlClient;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using Mobile_Crypto_Chat_Server.Properties;

namespace Mobile_Crypto_Chat_Server
{
	[ServiceContract]
	public class MobileCryptoChatService
	{
		private const int SHA1_LENGTH = 40;
		private const int SQL_ERROR_DUPLICATE_KEY = 2601;
		private const string SESSION_KEY_CHARS = 
			"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
		private const int SESSION_KEY_LEN = 50;
		private static readonly Random rand = new Random();

		[WebInvoke(Method = "POST", UriTemplate = "register", 
			BodyStyle=WebMessageBodyStyle.WrappedRequest)]
		public LoginResponse RegisterNewUser(string msisdn, string authCode)
		{
			CheckMSISDN(msisdn);
			CheckAuthCode(authCode);
			LoginResponse loginResult = null;
			ExecuteAndHandleExceptions(() => 
			{
				// Register the user in the DB
				CryptoChatEntities context = new CryptoChatEntities();
				User user = new User();
				user.MSISDN = msisdn.ToLowerInvariant();
				user.AuthCodeSHA1 = authCode;
				context.Users.AddObject(user);
				context.SaveChanges();

				// Login the user into the system after the registration
				loginResult = LoginUser(msisdn, authCode);

				// Send the "user online" notification to all online users
				SendMessageToAllOnlineUsers(user,
					MessageType.MSG_USER_ONLINE, "New user registered in the system.");
			});
			return loginResult;
		}

		[WebInvoke(Method = "POST", UriTemplate = "login", 
			BodyStyle=WebMessageBodyStyle.WrappedRequest)]
		private LoginResponse LoginUser(string msisdn, string authCode)
		{
			LoginResponse loginResult = null;
			ExecuteAndHandleExceptions(() =>
			{
				CheckMSISDN(msisdn);
				CheckAuthCode(authCode);

				// Check whether the MSISDN and authCode are correct
				CryptoChatEntities context = new CryptoChatEntities();
				var user =
					(from u in context.Users
					 where u.MSISDN == msisdn && u.AuthCodeSHA1 == authCode
					 select u).FirstOrDefault();
				if (user == null)
				{
					throw new ErrorResponseException(HttpStatusCode.Forbidden,
						"ERR_INV_LOGIN", "Invalid MSISDN or password.");
				}

				// User found in the database -> perform login
				user.SessionKey = GenerateNewSessionKey(user.UserId);
				user.LastActivity = DateTime.Now;
				context.SaveChanges();

				// Delete all messages waiting at the server for the just logged user
				DeleteWaitingMessagesAndSessions(user);

				// Send the "user online" notification to all online users
				SendMessageToAllOnlineUsers(user,
					MessageType.MSG_USER_ONLINE, "User logged in the system.");

				loginResult = new LoginResponse() { SessionID = user.SessionKey };
			});
			return loginResult;
		}

		[WebInvoke(Method = "GET", UriTemplate = "logout/{sessionID}")]
		public StatusResponse LogoutUser(string sessionID)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User user = CheckUserSession(sessionID);

				CryptoChatEntities context = new CryptoChatEntities();
				int updatedSessionsCount = context.ExecuteStoreCommand(
					"UPDATE Users SET SessionKey=null WHERE SessionKey={0}",
					new object[] { sessionID });

				if (updatedSessionsCount != 1)
				{
					throw new ErrorResponseException(HttpStatusCode.InternalServerError,
						"ERR_LOGOUT_FAIL", "Logout failed unexpectedly.");
				}

				// Session sucessfully removed from DB (logout was successfull)
				DeleteWaitingMessagesAndSessions(user);

				// Send the "user online" notification to all online users
				SendMessageToAllOnlineUsers(user,
					MessageType.MSG_USER_OFFLINE, "User left the system.");
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "GET", UriTemplate = "list-users/{sessionID}")]
		public string[] ListUsers(string sessionID)
		{
			string[] allMSISDNs = null;
			ExecuteAndHandleExceptions(() =>
			{
				CheckUserSession(sessionID);

				DateTime minAllowedLastActivityTime =
					DateTime.Now.AddSeconds(-Settings.Default.HttpSessionTimeoutSeconds);
				CryptoChatEntities context = new CryptoChatEntities();
				allMSISDNs =
					(from u in context.Users
					 where u.LastActivity >= minAllowedLastActivityTime
					 orderby u.MSISDN
					 select u.MSISDN).ToArray();
			});
			return allMSISDNs;
		}

		[WebInvoke(Method = "POST", UriTemplate = "invite-user",
			BodyStyle = WebMessageBodyStyle.WrappedRequest)]
		public StatusResponse InviteUser(string sessionID, 
			string recipientMSISDN, string challenge)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User senderUser = CheckUserSession(sessionID);

				CheckMSISDN(recipientMSISDN);
				User recipientUser = CheckIfUserIsOnline(recipientMSISDN);
				CheckChallengeCode(challenge);

				if (senderUser.UserId == recipientUser.UserId)
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						"ERR_AUTO_CHAT", "Users cannot send chat invitations to themselves.");
				}

				DeleteChatSession(senderUser, recipientUser);

				CryptoChatEntities context = new CryptoChatEntities();

				// Create a new chat session between the users
				ChatSession newChatSession = new ChatSession();
				newChatSession.FromUserId = senderUser.UserId;
				newChatSession.ToUserId = recipientUser.UserId;
				newChatSession.ChatState = ChatSessionState.CHALLENGE_SENT.ToString();
				context.ChatSessions.AddObject(newChatSession);
				context.SaveChanges();

				// Send the challenge to the recipient user (through a message)
				SendMessage(senderUser, recipientUser, MessageType.MSG_CHALLENGE, challenge);
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "POST", UriTemplate = "response-chat-invitation",
			BodyStyle = WebMessageBodyStyle.WrappedRequest)]
		public StatusResponse ResponseChat(string sessionID,
			string recipientMSISDN, string response)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User senderUser = CheckUserSession(sessionID);

				CheckMSISDN(recipientMSISDN);
				User recipientUser = CheckIfUserIsOnline(recipientMSISDN);
				CheckResponseCode(response);

				CryptoChatEntities context = new CryptoChatEntities();

				// Find the existing chat session between the users
				var existingChatSession =
					(from s in context.ChatSessions
					 where s.FromUserId == recipientUser.UserId && s.ToUserId == senderUser.UserId
					 select s).FirstOrDefault();

				if (existingChatSession == null ||
					existingChatSession.ChatState != ChatSessionState.CHALLENGE_SENT.ToString())
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						"ERR_INVALID_STATE", "No open invitation exists from " +
						senderUser.MSISDN + " to " + recipientUser.MSISDN + ".");
				}

				// Change the state of the chat session to "response sent"
				existingChatSession.ChatState = ChatSessionState.RESPONSE_SENT.ToString();
				context.SaveChanges();

				// Send the response to the recipient user (through a message)
				SendMessage(senderUser, recipientUser, MessageType.MSG_RESPONSE, response);
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "POST", UriTemplate = "start-chat",
					BodyStyle = WebMessageBodyStyle.WrappedRequest)]
		public StatusResponse StartChat(string sessionID, string recipientMSISDN)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User senderUser = CheckUserSession(sessionID);

				CheckMSISDN(recipientMSISDN);
				User recipientUser = CheckIfUserIsOnline(recipientMSISDN);

				CryptoChatEntities context = new CryptoChatEntities();

				// Find the existing chat session between the users
				var existingChatSession =
					(from s in context.ChatSessions
					 where s.FromUserId == senderUser.UserId && s.ToUserId == recipientUser.UserId
					 select s).FirstOrDefault();

				if (existingChatSession == null ||
					existingChatSession.ChatState != ChatSessionState.RESPONSE_SENT.ToString())
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						"ERR_INVALID_STATE", "No accepted response exists from " +
						recipientUser.MSISDN + " to " + senderUser.MSISDN + ".");
				}

				// Change the state of the chat session to "response sent"
				existingChatSession.ChatState = ChatSessionState.ACTIVE.ToString();
				context.SaveChanges();

				// Send a notification to the recipient user (through a message)
				SendMessage(senderUser, recipientUser, MessageType.MSG_START_CHAT, "Chat started");
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "POST", UriTemplate = "cancel-chat",
			BodyStyle = WebMessageBodyStyle.WrappedRequest)]
		public StatusResponse CancelChat(string sessionID, string recipientMSISDN)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User senderUser = CheckUserSession(sessionID);

				CheckMSISDN(recipientMSISDN);
				User recipientUser = CheckIfUserIsOnline(recipientMSISDN);

				CryptoChatEntities context = new CryptoChatEntities();

				// Find the existing chat session between the users
				var existingChatSession =
					(from s in context.ChatSessions
					 where (s.FromUserId == senderUser.UserId && s.ToUserId == recipientUser.UserId) ||
						(s.FromUserId == recipientUser.UserId && s.ToUserId == senderUser.UserId)
					 select s).FirstOrDefault();

				if (existingChatSession == null)
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						"ERR_INVALID_STATE", "No active chat session exists between " +
						senderUser.MSISDN + " and " + recipientUser.MSISDN + ".");
				}

				// Delete the chat session
				DeleteChatSession(senderUser, recipientUser);

				// Send a notification to the recipient user (through a message)
				SendMessage(senderUser, recipientUser, MessageType.MSG_CANCEL_CHAT, "Chat canceled");
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "POST", UriTemplate = "send-chat-message",
			BodyStyle = WebMessageBodyStyle.WrappedRequest)]
		public StatusResponse SendChatMessage(
			string sessionID, string recipientMSISDN, string encryptedMsg)
		{
			ExecuteAndHandleExceptions(() =>
			{
				User senderUser = CheckUserSession(sessionID);

				CheckMSISDN(recipientMSISDN);
				User recipientUser = CheckIfUserIsOnline(recipientMSISDN);
				CheckEncryptedMessage(encryptedMsg);

				CryptoChatEntities context = new CryptoChatEntities();

				// Find the existing chat session between the users
				var existingChatSession =
					(from s in context.ChatSessions
					 where (s.FromUserId == senderUser.UserId && s.ToUserId == recipientUser.UserId) ||
						(s.FromUserId == recipientUser.UserId && s.ToUserId == senderUser.UserId)
					 select s).FirstOrDefault();

				if (existingChatSession == null || 
					existingChatSession.ChatState != ChatSessionState.ACTIVE.ToString())
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						"ERR_INVALID_STATE", "No active chat session exists between " +
						senderUser.MSISDN + " and " + recipientUser.MSISDN + ".");
				}

				// Send the chat message to the recipient user
				SendMessage(senderUser, recipientUser, MessageType.MSG_CHAT_MESSAGE, encryptedMsg);
			});
			return new StatusResponse();
		}

		[WebInvoke(Method = "GET", UriTemplate = "get-next-message/{sessionID}")]
		public MessageResponse GetNextMessage(string sessionID)
		{
			MessageResponse messageToReturn = null;
			ExecuteAndHandleExceptions(() =>
			{
				User user = CheckUserSession(sessionID);

				CryptoChatEntities context = new CryptoChatEntities();
				Message nextMsg = 
					(from m in context.Messages.Include("FromUser")
					where m.ToUserId == user.UserId
					orderby m.MsgDate
					select m).FirstOrDefault();
				if (nextMsg != null)
				{
					// A message is found --> return it as a result and delete it from DB
					messageToReturn = new MessageResponse()
					{
						MsgType = nextMsg.MsgType,
						MsgText = nextMsg.MsgText,
						Msisdn = nextMsg.FromUser.MSISDN
					};
					context.DeleteObject(nextMsg);
					context.SaveChanges();
				}
				else
				{
					// No messages are waiting --> return MSG_NO_MESSAGES
					messageToReturn = new MessageResponse() 
					{ 
						MsgType = MessageType.MSG_NO_MESSAGES.ToString() 
					};
				}
			});

			return messageToReturn;
		}

		private void SendMessage(User senderUser, User recipientUser, 
			MessageType messageType, string msgText)
		{
			Message msg = new Message();
			msg.FromUserId = senderUser.UserId;
			msg.ToUserId = recipientUser.UserId;
			msg.MsgType = messageType.ToString();
			msg.MsgDate = DateTime.Now;
			msg.MsgText = msgText;
			CryptoChatEntities context = new CryptoChatEntities();
			context.Messages.AddObject(msg);
			context.SaveChanges();
		}

		internal static void SendMessageToAllOnlineUsers(
			User senderUser, MessageType msgType, string msgText)
		{
			DateTime minAllowedLastActivityTime =
				DateTime.Now.AddSeconds(-Settings.Default.HttpSessionTimeoutSeconds);
			CryptoChatEntities context = new CryptoChatEntities();
			var allUserIds =
				from u in context.Users
				where u.LastActivity >= minAllowedLastActivityTime
				select u.UserId;
			foreach (var recipientUserId in allUserIds)
			{
				if (senderUser.UserId != recipientUserId)
				{
					Message msg = new Message();
					msg.FromUserId = senderUser.UserId;
					msg.ToUserId = recipientUserId;
					msg.MsgType = msgType.ToString();
					msg.MsgText = msgText;
					msg.MsgDate = DateTime.Now;
					context.Messages.AddObject(msg);
				}
			}
			context.SaveChanges();
		}

		private void DeleteWaitingMessagesAndSessions(User recipientUser)
		{
			CryptoChatEntities context = new CryptoChatEntities();

			// Delete any waiting messages for the user
			string deleteMessagesSQL = "DELETE FROM Messages WHERE ToUserId={0}";
			context.ExecuteStoreCommand(deleteMessagesSQL,
				new object[] { recipientUser.UserId });

			// Delete any existing chat sessions associated with the user
			string deleteChatSessionsSQL = "DELETE FROM ChatSessions WHERE " +
				"FromUserId={0} or ToUserId={0}";
			context.ExecuteStoreCommand(deleteChatSessionsSQL,
				new object[] { recipientUser.UserId });
		}

		private void DeleteChatSession(User senderUser, User recipientUser)
		{
			CryptoChatEntities context = new CryptoChatEntities();

			// Delete any existing chat sessions between the users
			string deleteChatSessionsSQL = "DELETE FROM ChatSessions WHERE " +
				"(FromUserId={0} and ToUserId={1}) or (FromUserId={1} and ToUserId={0})";
			context.ExecuteStoreCommand(deleteChatSessionsSQL,
				new object[] { senderUser.UserId, recipientUser.UserId });

			// Delete any waiting chat messages between the users
			string msgTypesToDelete =
				"'" + MessageType.MSG_CHALLENGE.ToString() + "'," +
				"'" + MessageType.MSG_RESPONSE.ToString() + "'," +
				"'" + MessageType.MSG_START_CHAT.ToString() + "'," +
				"'" + MessageType.MSG_CANCEL_CHAT.ToString() + "'," +
				"'" + MessageType.MSG_CHAT_MESSAGE.ToString() + "'";
			string deleteMessagesSQL = "DELETE FROM Messages WHERE " +
				"((FromUserId={0} and ToUserId={1}) or (FromUserId={1} and ToUserId={0})) AND " +
				"(MsgType in (" + msgTypesToDelete + "))";
			context.ExecuteStoreCommand(deleteMessagesSQL,
				new object[] { senderUser.UserId, recipientUser.UserId });
		}

		private void CheckChallengeCode(string challenge)
		{
			if (!IsBase64String(challenge))
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound, "ERR_BAD_CHALL",
					"The specified challenge is invalid. Use BASE64-encoded AES-128 code.");
			}
		}

		private void CheckResponseCode(string response)
		{
			if (!IsBase64String(response))
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound, "ERR_BAD_RESP",
					"The specified response is invalid. Use BASE64-encoded AES-128 code.");
			}
		}

		private void CheckEncryptedMessage(string encryptedMsg)
		{
			if (!IsBase64String(encryptedMsg))
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound, "ERR_BAD_MSG",
					"The specified encrypted message is invalid. Use BASE64-encoded AES-128 code.");
			}
		}

		private bool IsBase64String(string text)
		{
			return Regex.IsMatch(text, @"^[0-9a-zA-Z+/=]+$");
		}

		private User CheckIfUserIsOnline(string recipientMSISDN)
		{
			CryptoChatEntities context = new CryptoChatEntities();
			DateTime minAllowedLastActivityTime =
				DateTime.Now.AddSeconds(-Settings.Default.HttpSessionTimeoutSeconds);
			User user =
				(from u in context.Users
				 where u.MSISDN == recipientMSISDN					
				 select u).FirstOrDefault();
			if (user == null)
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound,
					"ERR_BAD_USER", "The specified user does not exists.");
			}
			if (user.LastActivity < minAllowedLastActivityTime)
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound,
					"ERR_USER_OFF", "The specified user is offline.");
			}
			return user;
		}

		/// <summary>
		/// Executes a piece of code and processes any exceptions raised. In case of
		/// exception it will be transformed to ErrorResponseException and will be
		/// returned as HTTP error code + HTTP response body containing JSON with
		/// error code + error message.
		/// </summary>
		private void ExecuteAndHandleExceptions(Action codeToExecute)
		{
			try
			{
				codeToExecute();
			}
			catch (Exception ex)
			{
				if (ex is ErrorResponseException)
				{
					throw ex;
				}
				
				CheckForDuplicateKey(ex, "ERR_DUPLICATE", "Duplicated record.");
				
				throw new ErrorResponseException(HttpStatusCode.InternalServerError,
					"ERR_GENERAL", "Unexpected error occurred: " + ex);
			}
		}

		private void CheckForDuplicateKey(Exception ex, string errorCode, string errorMsg)
		{
			if (ex.InnerException != null && ex.InnerException is SqlException)
			{
				SqlException sqlEx = (SqlException)ex.InnerException;
				if (sqlEx.Number == SQL_ERROR_DUPLICATE_KEY)
				{
					throw new ErrorResponseException(HttpStatusCode.NotFound,
						errorCode, errorMsg);
				}
			}
		}

		private void CheckMSISDN(string msisdn)
		{
			if (!Regex.IsMatch(msisdn, @"^\+[0-9]{3,20}$"))
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound,
					"ERR_USR_NAME", "Invalid MSISDN.");
			}
		}
		private void CheckAuthCode(string authCode)
		{
			if (!Regex.IsMatch(authCode, @"^[0-9a-f]+$") ||
				authCode.Length != SHA1_LENGTH)
			{
				throw new ErrorResponseException(HttpStatusCode.NotFound,
					"ERR_AUTH_CODE", "Invalid authCode. It should be exactly " +
					SHA1_LENGTH + " hexadecimal chars.");
			}
		}

		private User CheckUserSession(string sessionID)
		{
			CryptoChatEntities context = new CryptoChatEntities();
			DateTime minAllowedLastActivityTime =
				DateTime.Now.AddSeconds(-Settings.Default.HttpSessionTimeoutSeconds);
			var dbUser =
				(from u in context.Users
					where u.SessionKey == sessionID &&
					u.LastActivity >= minAllowedLastActivityTime
					select u).FirstOrDefault();
			if (dbUser == null)
			{
				// The session is either too old or does not exist
				throw new ErrorResponseException(HttpStatusCode.Forbidden,
					"ERR_SESSIONID", "Invalid sessionID.");
			}
			// The user session is valid -> update the last activity time
			dbUser.LastActivity = DateTime.Now;
			context.SaveChanges();

			return dbUser;
		}

		private string GenerateNewSessionKey(int userId)
		{
			StringBuilder keyChars = new StringBuilder(50);
			keyChars.Append(userId.ToString());
			while (keyChars.Length < SESSION_KEY_LEN)
			{
				int randomCharNum;
				lock (rand)
				{
					randomCharNum = rand.Next(SESSION_KEY_CHARS.Length);
				}
				char randomKeyChar = SESSION_KEY_CHARS[randomCharNum];
				keyChars.Append(randomKeyChar);				
			}
			string sessionKey = keyChars.ToString();
			return sessionKey;
		}
	}
}
