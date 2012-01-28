using System.Runtime.Serialization;

namespace Mobile_Crypto_Chat_Server
{
	[DataContract]
	public class StatusResponse
	{
		[DataMember(Name = "status")]
		public string Status { get; set; }

		public StatusResponse()
		{
			this.Status = "OK";
		}
	}
}
