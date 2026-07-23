# Order Processing Platform API

[![CI](https://github.com/PetkovskiM/order-processing-platform-api/actions/workflows/ci.yml/badge.svg)](https://github.com/PetkovskiM/order-processing-platform-api/actions/workflows/ci.yml)

A portfolio and learning project built with ASP.NET Core Web API and Entity Framework Core.

The application demonstrates practical backend-development concepts commonly expected from a mid-level .NET developer, including REST API design, relational data modeling, business-rule validation, transactions, structured logging, background processing, CQRS, automated testing, and continuous integration.

## Project Goals

The project was created to practise building a business-oriented API rather than a basic CRUD demonstration.

Its main goals are to demonstrate:

* Clean and maintainable ASP.NET Core code
* RESTful endpoint design
* EF Core relationships and Fluent API configuration
* Business rules and transaction management
* Consistent API error responses
* Structured application logging
* Audit logging
* Background processing
* CQRS and MediatR
* Unit and integration testing
* GitHub-based feature development and continuous integration

## Technology Stack

* .NET 10
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* SQLite in-memory database for integration tests
* MediatR
* Serilog
* `Channel<T>` and `BackgroundService`
* xUnit
* `WebApplicationFactory`
* GitHub Actions
* Swagger / OpenAPI

## Main Features

### Customer Management

The API supports creating, reading, updating, and deleting customers.

Customer email addresses are protected by both application validation and a unique database index.

### Product Management

The API supports creating, reading, updating, and deleting products.

Products contain:

* SKU
* Name
* Description
* Price
* Available stock
* Creation and update timestamps

SKU values are protected by a unique database index.

### Order Creation

Creating an order includes several business rules:

* The customer must exist.
* Every product must exist.
* Product IDs cannot be duplicated within the same request.
* Quantities must be greater than zero.
* Sufficient stock must be available.
* Product names and prices are stored as order-item snapshots.
* Stock is reduced when the order is created.
* The total amount is calculated by the application.
* An audit entry is created.
* The operation is executed inside an EF Core transaction.

### Order Lifecycle

An order can move from `Pending` to:

* `Completed`
* `Cancelled`

Completed or cancelled orders cannot be changed to another final status.

Cancelling a pending order restores the reserved product stock.

### Filtering, Sorting and Pagination

The order-list endpoint supports:

* Pagination
* Customer filtering
* Status filtering
* Creation-date filtering
* Sorting
* Deterministic secondary ordering

Responses contain pagination metadata such as:

* Current page
* Page size
* Total record count
* Total pages
* Previous-page availability
* Next-page availability

### Consistent Error Responses

The API uses centralized exception handling and returns RFC-style `ProblemDetails` responses.

Error responses include:

* HTTP status
* Error title
* Error details
* Application error code
* Trace identifier
* UTC timestamp
* Validation errors when applicable

### Structured Logging

Serilog is used for:

* HTTP request logging
* Order lifecycle events
* Business-operation logging
* Background email processing
* Error diagnostics

Structured properties such as `OrderId`, `CustomerId`, and `Recipient` are logged separately instead of being embedded only inside plain text.

### Audit Logging

Order creation, completion, and cancellation produce audit records.

Audit entries include:

* Entity name
* Entity ID
* Action
* Previous values
* New values
* UTC timestamp

The audit service adds audit entities to the shared EF Core unit of work, while the calling application service or handler controls `SaveChangesAsync` and transaction boundaries.

### Background Email Processing

Order-created and order-completed notifications are placed into a bounded in-memory queue implemented with `Channel<EmailMessage>`.

A hosted `BackgroundService` consumes queued messages and resolves the scoped email sender through a dependency-injection scope.

Current flow:

```text
HTTP request
    ↓
Persist order changes
    ↓
Queue email message
    ↓
Return API response

Background worker
    ↓
Dequeue message
    ↓
Send or simulate email
```

The current email sender writes structured logs instead of contacting a real email provider.

The in-memory queue improves request latency but is not durable. Messages can be lost if the API stops before processing them.

A future production design would use:

* Transactional outbox
* RabbitMQ or another external broker
* Separate worker process
* Retries
* Dead-letter queue
* Idempotent message processing

## Architecture

The API currently uses a pragmatic hybrid architecture.

### Service-Based Features

Customer, product, and some order operations use:

```text
Controller
    ↓
Application service
    ↓
EF Core DbContext
    ↓
SQL Server
```

### CQRS Features

Selected order operations use MediatR and dedicated handlers:

```text
Controller
    ↓
ISender
    ↓
Command or query handler
    ↓
EF Core DbContext
```

Current CQRS slices include:

* `GetOrderByIdQuery`
* `CompleteOrderCommand`

The query handler uses `AsNoTracking` and DTO projection.

The command handler loads tracked entities, validates business rules, updates state, creates an audit entry, saves changes, and queues an email.

This incremental approach demonstrates CQRS without forcing unnecessary abstraction onto every simple CRUD operation.

## Project Structure

```text
OrderProcessingPlatform
│
├── OrderProcessing.Api
│   ├── BackgroundJobs
│   ├── Controllers
│   ├── Data
│   ├── DTOs
│   ├── Entities
│   ├── Exceptions
│   ├── Features
│   │   └── Orders
│   │       ├── Commands
│   │       │   └── CompleteOrder
│   │       └── Queries
│   │           └── GetOrderById
│   ├── Middleware
│   ├── Services
│   │   ├── Auditing
│   │   ├── Customers
│   │   ├── Emailing
│   │   ├── Orders
│   │   └── Products
│   └── Validation
│
├── tests
│   └── OrderProcessing.Api.Tests
│       ├── Infrastructure
│       ├── Integration
│       └── Unit
│
└── .github
    └── workflows
        └── ci.yml
```

## Running the Application

### Prerequisites

Install:

* .NET 10 SDK
* SQL Server or SQL Server LocalDB


### Apply Migrations

```bash
dotnet ef database update --project OrderProcessing.Api
```

## API Endpoints

### Customers

```text
GET    /api/customers
GET    /api/customers/{id}
POST   /api/customers
PUT    /api/customers/{id}
DELETE /api/customers/{id}
```

### Products

```text
GET    /api/products
GET    /api/products/{id}
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}
```

### Orders

```text
GET   /api/orders
GET   /api/orders/{id}
POST  /api/orders
PATCH /api/orders/{id}/complete
PATCH /api/orders/{id}/cancel
```

### Diagnostics

```text
GET /api/health
```


The test suite includes:

### Unit Tests

Unit tests cover isolated logic such as:

* Custom whitespace validation
* Pagination metadata calculations

### Integration Tests

Integration tests use `WebApplicationFactory` to start the real ASP.NET Core application pipeline.

The production SQL Server registration is replaced with an open SQLite in-memory database.

Integration tests cover:

* Health endpoint
* Model validation
* Consistent error responses
* Missing resources
* Order creation
* Stock reduction
* Response status codes
* Response headers
* Database changes

The SQLite connection remains open for the test-host lifetime so the in-memory database is retained while tests execute.

## Continuous Integration

GitHub Actions automatically performs:

```text
Restore
→ Release build
→ Unit tests
→ Integration tests
→ Test-result upload
```

The workflow runs for:

* Pull requests targeting `main`
* Pushes to `main`

A failed build or test causes the workflow to fail.

## Important Design Decisions

### DbContext as Unit of Work

EF Core’s `DbContext` tracks changes across multiple entities and persists them through one `SaveChangesAsync` call.

It therefore acts as the unit of work for this application.

Separate generic repository abstractions were not added because EF Core already provides repository-like access through `DbSet<T>` and supports queries, tracking, transactions, and change persistence directly.

### Projection for Read Operations

Read-only endpoints use:

```csharp
AsNoTracking()
```

and project directly to response DTOs.

This avoids unnecessary entity tracking and prevents exposing EF Core entities as API contracts.

### Explicit Transactions for Multi-Step Workflows

Order creation uses an explicit transaction because it includes:

* Order creation
* Order-item creation
* Stock updates
* Audit logging
* Multiple save operations

If any required step fails before the transaction commits, the complete operation is rolled back.

### Database Constraints as Final Protection

Application validation provides useful error messages, but the database remains the final consistency boundary.

The model includes:

* Unique indexes
* Required columns
* Foreign keys
* Delete behaviors
* Decimal precision
* Check constraints

### Email as a Secondary Side Effect

An order is considered successfully created or completed when the database operation succeeds.

Email queue failures are logged but do not convert an already committed business operation into a misleading HTTP failure response.

For guaranteed notification delivery, the next architectural step would be the transactional outbox pattern.

## Current Limitations

The current implementation intentionally has several limitations:

* Email delivery is simulated through logging.
* The in-memory email queue is not durable.
* RabbitMQ is designed but not yet implemented.
* The transactional outbox is not yet implemented.
* CQRS is applied only to selected order use cases.
* SQLite integration tests do not guarantee complete SQL Server provider parity.
* Demo seed data should be separated from production and test initialization before a real production release.
* Authentication and authorization are not yet implemented.

## Planned Improvements

Potential next steps include:

1. Move integration-test data to a dedicated test seeder.
2. Move development demo data out of model-managed production seeding.
3. Complete the CQRS migration for all order operations.
4. Add FluentValidation through a MediatR pipeline behavior.
5. Add transactional outbox storage.
6. Add RabbitMQ and a separate email worker.
7. Add retry and dead-letter handling.
8. Add idempotent message consumption.
9. Add authentication and authorization.
10. Add Docker support.
11. Add SQL Server-based integration tests.
12. Deploy the API to a cloud platform.
13. Add metrics and distributed tracing.

## Interview Summary

This project demonstrates how I approach a business-oriented ASP.NET Core API.

I use controllers as the HTTP boundary and keep business rules in application services or CQRS handlers. EF Core handles relational persistence, change tracking, and transaction management. Read operations use `AsNoTracking` and DTO projection, while commands use tracked entities and explicit transactions where multiple related operations must succeed together.

The API has centralized validation and exception handling, structured Serilog logging, audit records, pagination and filtering, asynchronous background email processing, MediatR-based command and query handlers, automated unit and integration tests, and a GitHub Actions continuous-integration workflow.

I also understand the current architectural limitations. The in-memory channel is not durable, and directly publishing after a database commit would create a dual-write problem. For stronger production reliability, I would use a transactional outbox, RabbitMQ, a separate worker, manual acknowledgements, retries, dead-lettering, and idempotent consumers.

## Project Status

The initial implementation roadmap is complete.

The project now provides a strong foundation for further work in:

* Distributed messaging
* Cloud deployment
* Security
* Advanced testing
* SQL optimization
* Observability
* Scalable architecture
