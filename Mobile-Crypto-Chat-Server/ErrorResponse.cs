using System.Runtime.Serialization;

namespace Mobile_Crypto_Chat_Server
{
	[DataContract]
	public class ErrorResponse
	{
		[DataMember(Name = "errorCode")]
		public string ErrorCode { get; set; }

		[DataMember(Name = "errorMsg", EmitDefaultValue=false)]
		public string ErrorMsg { get; set; }
	}
}