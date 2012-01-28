using System.Runtime.Serialization;
namespace Mobile_Crypto_Chat_Server
{
	[DataContract]
	public class LoginResponse
	{
		[DataMember(Name = "sessionID")]
		public string SessionID { get; set; }
	}
}
