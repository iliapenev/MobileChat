using System;
using System.Web;
using System.Timers;
using System.Linq;
using Mobile_Crypto_Chat_Server.Properties;

namespace Mobile_Crypto_Chat_Server
{
	public class Global : System.Web.HttpApplication
	{
		private static Timer cleanupServiceTimer;

		protected void Application_Start(object sender, EventArgs e)
		{
			cleanupServiceTimer = new Timer(1000 * Settings.Default.SessionCleanupSeconds);
			cleanupServiceTimer.AutoReset = true;
			cleanupServiceTimer.Enabled = true;
			cleanupServiceTimer.Elapsed += new ElapsedEventHandler(cleanupServiceTimer_Elapsed);
		}

		void cleanupServiceTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			DeleteAllExpiredSessionsAndMessages();
		}

		private void DeleteAllExpiredSessionsAndMessages()
		{
			CryptoChatEntities context = new CryptoChatEntities();

			DateTime minAllowedLastActivityTime =
				DateTime.Now.AddSeconds(-Settings.Default.HttpSessionTimeoutSeconds);

			// Delete any expired chat sessions in the DB
			string deleteChatSessionsSQL = 
				"DELETE FROM ChatSessions " +
				"FROM Users u JOIN ChatSessions s " +
				"  ON (u.UserId = s.FromUserId or u.UserId = s.ToUserId) " +
				"WHERE LastActivity < {0}";
			context.ExecuteStoreCommand(deleteChatSessionsSQL,
				new object[] { minAllowedLastActivityTime });

			// Delete any expired messages in the DB
			string deleteMessagesSQL =
				"DELETE FROM Messages " +
				"FROM Users u JOIN Messages m ON (u.UserId = m.ToUserId) " +
				"WHERE LastActivity < {0}";
			context.ExecuteStoreCommand(deleteMessagesSQL,
				new object[] { minAllowedLastActivityTime });

			// Send notifications about all users whose sessions were expired
			var usersWithExpiredSession =
				from u in context.Users
				where u.LastActivity < minAllowedLastActivityTime &&
					u.SessionKey != null
				select u;
			foreach (var user in usersWithExpiredSession)
			{
				user.SessionKey = null;
				MobileCryptoChatService.SendMessageToAllOnlineUsers(user,
					MessageType.MSG_USER_OFFLINE, "User lost connection with the server.");
			}
			context.SaveChanges();
		}

		protected void Application_BeginRequest(object sender, EventArgs e)
		{
			HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			HttpContext.Current.Response.Cache.SetNoStore();

			EnableCrossDomainAjaxCalls();
		}

		protected void Application_Error(object sender, EventArgs e)
		{
		}

		protected void Application_End(object sender, EventArgs e)
		{
			cleanupServiceTimer.Enabled = false;
		}

		private void EnableCrossDomainAjaxCalls()
		{
			HttpContext.Current.Response.AddHeader("Access-Control-Allow-Origin", "*");

			if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
			{
				HttpContext.Current.Response.AddHeader(
					"Access-Control-Allow-Methods", "GET, POST");
				HttpContext.Current.Response.AddHeader(
					"Access-Control-Allow-Headers", "Content-Type, Accept");
				HttpContext.Current.Response.AddHeader(
					"Access-Control-Max-Age", "1728000");
				HttpContext.Current.Response.End();
			}
		}
	}
}
