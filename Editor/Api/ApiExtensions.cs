using System;
using System.Net;

namespace FVPR
{
	public static class ApiExtensions
	{
		public static string ToApiString(this TicketType type)
		{
			switch (type)
			{
				case TicketType.PublishPackage:		return "package.publish";
				case TicketType.DeprecatePackage:	return "package.deprecate";
				default:							throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
		}

		public static TicketStatus GetStatus(this TicketResponse response)
		{
			switch (response.Status)
			{
				case "AwaitingApproval":	return TicketStatus.AwaitingApproval;
				case "Approved":			return TicketStatus.Approved;
				case "Rejected":			return TicketStatus.Rejected;
				case "Expired":				return TicketStatus.Expired;
				case "Completed":			return TicketStatus.Completed;
				default:					return TicketStatus.UNKNOWN;
			}
		}
		
		public static bool Is(this ErrorResponse error, HttpStatusCode code) => error.Code == (int) code;
	}
}