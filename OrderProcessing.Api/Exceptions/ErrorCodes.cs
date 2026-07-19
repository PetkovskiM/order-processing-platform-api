namespace OrderProcessing.Api.Exceptions;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string BadRequest = "bad_request";
    public const string ResourceNotFound = "resource_not_found";
    public const string Conflict = "conflict";
    public const string InternalServerError = "internal_server_error";
    public const string InsufficientStock = "insufficient_stock";
    public const string InvalidOrderStatus = "invalid_order_status";
    public const string DuplicateOrderProducts = "duplicate_order_products";
    public const string InvalidDateRange = "invalid_date_range";
}