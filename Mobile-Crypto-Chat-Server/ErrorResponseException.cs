using System.ServiceModel.Web;
using System.Net;

namespace Mobile_Crypto_Chat_Server
{
	public class ErrorResponseException : WebFaultException<ErrorResponse>
	{
		public ErrorResponseException(HttpStatusCode httpStatusCode, string errorCode, string errorMsg) :
			base(new ErrorResponse() { ErrorCode = errorCode, ErrorMsg = errorMsg }, httpStatusCode)
		{
		}
	}
}