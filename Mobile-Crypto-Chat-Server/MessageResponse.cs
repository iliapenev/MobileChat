using System.Runtime.Serialization;

namespace Mobile_Crypto_Chat_Server
{
	[DataContract]
	public class MessageResponse
	{
		[DataMember(Name = "msgType")]
		public string MsgType { get; set; }

		[DataMember(Name = "msisdn", EmitDefaultValue = false)]
		public string Msisdn { get; set; }

		[DataMember(Name = "msgText", EmitDefaultValue = false)]
		public string MsgText { get; set; }
	}
}