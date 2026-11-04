---
title: 'Registration and login rejections'
type: 'feature'
ticket: '2'
created: '2026-11-04'
status: built
baseline_revision: 'e2ba83a'
route: 'oneshot'
route_source: 'pinned'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-apiplayground-mvp/architecture-apiplayground-backend/architecture-apiplayground-backend.md'
  - 'backend/src/ApiPlayground.Application/Services/AuthUseCases.cs'
  - 'backend/src/ApiPlayground.Infrastructure/Auth/IdentityAccountPort.cs'
---

<frozen-after-approval>
## Intent

- A duplicate email returns the clear message "An account with this email already exists." with HTTP 400.
- Wrong password and unknown email return the same message, "Invalid email or password.", with HTTP 401.
- Passwords are stored only as hashes (Identity `UserManager.CreateAsync`).
- Password minimum is 8 characters, with no composition rules.
</frozen-after-approval>

## Implementation Notes

Route `oneshot`: the change touches one port, one use case, one Identity adapter, and one test. Risk is low and no new boundaries are added.

- `IAccountPort.RegisterAsync` returns `AccountRegistration(UserId, EmailTaken)`.
- `IdentityAccountPort` maps `DuplicateUserName` and `DuplicateEmail` from `IdentityResult.Errors` to `EmailTaken`.
- `RegisterAccountUseCase` returns `DuplicateEmailMessage` for a taken email. Other failures keep the generic message.
- `AuthEndpointTests.Register_rejects_invalid_input_and_duplicate_email` asserts the duplicate message.

## Verification

- `dotnet build ApiPlayground.sln --configuration Release`: succeeded.
- `dotnet test ApiPlayground.sln --configuration Release`: 198 passed, 0 failed.
- Quick review (context-free subagent): no findings.
