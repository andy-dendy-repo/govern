---
title: "ADR 0001: in-house mediator with Result responses and pipelines"
tags: [adr, mediator, cqrs, pipelines, result, validation]
code: [src/Govern.Utils/Mediator, src/Govern.Utils/Results, src/Govern.Application/DependencyInjection.cs]
updated: 2026-09-27
---

# ADR 0001: in-house mediator with Result responses and pipelines

## Status
Accepted (2026-09-27).

## Context
Endpoints should stay thin, use cases should be one class each, and cross-cutting behaviour (error handling, logging, validation) should apply to every use case without repetition. MediatR became commercially licensed, and the earlier PlainCV sample used a small in-house dispatcher (DispatchR). The draft `Govern.Utils.Mediator` could not return responses.

## Decision
Govern uses its own small mediator in `Govern.Utils.Mediator`:
- **Self-referencing requests.** `SignUpRequest : IRequest<SignUpRequest, SignUpResponse>` lets `SendAsync` infer both types and resolve `IRequestHandler<TRequest, TResponse>` directly, with no reflection per call.
- **Results, not exceptions, for expected failures.** Handlers return `ValueTask<Result<TResponse>>`, and `Error` carries an `ErrorType` that the Api maps to HTTP status codes.
- **Ordered pipelines** (`IPipeline<TRequest, TResponse>`), registered in `AddApplication`:
  1. `ErrorHandlingPipeline`: an unhandled exception becomes `Error.Unexpected`, logged once.
  2. `LoggingPipeline`: one Information entry per request, with its name, duration and outcome.
  3. `ValidationPipeline`: FluentValidation validators; a failure short-circuits with the field errors.
- **Notifications** (`INotification` / `INotificationHandler<T>`) are published by runtime type. Domain events use them after `SaveChangesAsync`.
- **Scope.** Handlers resolve from the caller's DI scope by default (`MediatorOptions.UseParentScope = true`), so scoped services like the current user are shared. `false` creates a child scope per call, like DispatchR did.
- **Registration.** Handlers are registered by assembly scan (`AddMediator`), including internal types.

## Consequences
- No licensing dependency, and a small, fully unit-tested surface (`tests/Govern.UnitTests/Mediator`).
- Every use case gets validation, logging and safe error handling automatically. Handlers contain only business logic.
- Handlers must not throw for business outcomes. This is enforced by review and `error-handling.md`.
- Pipelines are open generics, so a new cross-cutting concern (authorization, caching) is one class plus one `AddPipeline` line.
