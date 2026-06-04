# RPC Status Error Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the ambiguous `RpcStatus.Exception` status with a small stable framework error taxonomy and wire the current server/client behavior to it.

**Architecture:** Keep business failures in DTOs and keep non-`Ok` framework responses on the existing `RpcException` path. The implementation is a focused protocol/API change in `ULinkRPC.Core`, with server mappings updated in `ULinkRPC.Server`, tests updated before production code, and docs/version metadata kept in sync.

**Tech Stack:** C#/.NET, xUnit tests, ULinkRPC Core/Client/Server packages, Hugo blog markdown docs.

---

## File Structure

- Modify `tests/ULinkRPC.Tests/RpcEnvelopesTests.cs`: lock numeric values for `Ok`, `NotFound`, `HandlerError`, `Overloaded`, `BadRequest`, and `ProtocolError`; remove references to `Exception`.
- Modify `tests/ULinkRPC.Tests/RpcEnvelopeCodecTests.cs`: update response round-trip and golden-byte tests to use a concrete non-OK status such as `HandlerError`.
- Modify `tests/ULinkRPC.Tests/RpcServerTests.cs`: assert handler failures return `HandlerError` and request queue saturation returns `Overloaded`.
- Modify `tests/ULinkRPC.Tests/RpcClientTests.cs`: assert client `RpcException.Status` is `HandlerError` for server handler exceptions.
- Modify `src/ULinkRPC.Core/Protocol/RpcEnvelopes.cs`: replace `Exception` with the new status set and XML docs.
- Modify `src/ULinkRPC.Server/Dispatching/ServerRequestDispatcher.cs`: map handler failures and null responses to `HandlerError`; map request queue full to `Overloaded`.
- Modify `blog/content/posts/error-handling.md`: update the documented status taxonomy and client failure mode from `InvalidOperationException` to `RpcException`.
- Modify `blog/content/posts/api-stability-roadmap.md`: replace stale status notes with the accepted framework-only status model.
- Modify `blog/content/reference/api.md`: document `RpcStatus` and `RpcException` semantics if the reference page already lists related API types.
- Modify `CHANGELOG.md`: add a new top entry for the status model change.
- Modify `src/ULinkRPC.Core/ULinkRPC.Core.csproj`: bump Core from `0.11.11` to `0.11.12`.
- Modify `src/ULinkRPC.Server/ULinkRPC.Server.csproj`: bump Server from `0.11.10` to `0.11.11`.
- Modify `src/ULinkRPC.Starter/ReleaseVersions.json`: update `Core` to `0.11.12` and `Server` to `0.11.11`.
- Modify `tests/ULinkRPC.Starter.Tests/StarterTemplateGeneratorTests.cs`: update expected Core and Server package versions used by generated starter tests.

### Task 1: Write Failing Status Taxonomy Tests

**Files:**
- Modify: `tests/ULinkRPC.Tests/RpcEnvelopesTests.cs`
- Modify: `tests/ULinkRPC.Tests/RpcEnvelopeCodecTests.cs`
- Modify: `tests/ULinkRPC.Tests/RpcServerTests.cs`
- Modify: `tests/ULinkRPC.Tests/RpcClientTests.cs`

- [ ] **Step 1: Update enum value test before production code**

Replace the body of `RpcStatus_Values` in `tests/ULinkRPC.Tests/RpcEnvelopesTests.cs` with:

```csharp
Assert.Equal(0, (byte)RpcStatus.Ok);
Assert.Equal(1, (byte)RpcStatus.NotFound);
Assert.Equal(2, (byte)RpcStatus.HandlerError);
Assert.Equal(3, (byte)RpcStatus.Overloaded);
Assert.Equal(4, (byte)RpcStatus.BadRequest);
Assert.Equal(5, (byte)RpcStatus.ProtocolError);
```

In `RpcException_IsNotInvalidOperationException`, construct the exception with `RpcStatus.HandlerError`:

```csharp
Exception ex = new RpcException(
    RpcStatus.HandlerError,
    errorMessage: null,
    requestId: 1,
    serviceId: 2,
    methodId: 3);
```

- [ ] **Step 2: Update codec tests before production code**

In `tests/ULinkRPC.Tests/RpcEnvelopeCodecTests.cs`, replace response error test statuses with `RpcStatus.HandlerError`:

```csharp
Status = RpcStatus.HandlerError,
```

Update assertions:

```csharp
Assert.Equal(RpcStatus.HandlerError, decoded.Status);
```

Keep the golden byte status value as `0x02`, because `HandlerError = 2` intentionally occupies the old numeric slot.

- [ ] **Step 3: Update server behavior tests before production code**

Rename `HandlerThrows_ReturnsException` to:

```csharp
public async Task HandlerThrows_ReturnsHandlerError()
```

Change its status assertion to:

```csharp
Assert.Equal(RpcStatus.HandlerError, resp.Status);
```

In `RequestQueueLimit_RejectsRequestsBeyondBudget`, change the overload response assertion to:

```csharp
Assert.Equal(RpcStatus.Overloaded, overloadResponse.Status);
```

- [ ] **Step 4: Update client behavior test before production code**

In `tests/ULinkRPC.Tests/RpcClientTests.cs`, rename `CallAsync_ServerError_ThrowsOnClient` to:

```csharp
public async Task CallAsync_HandlerError_ThrowsRpcException()
```

Change the assertions to:

```csharp
Assert.Equal(RpcStatus.HandlerError, ex.Status);
Assert.Equal("RPC handler failed.", ex.ErrorMessage);
Assert.Equal(1, ex.ServiceId);
Assert.Equal(1, ex.MethodId);
Assert.Contains("HandlerError", ex.Message);
```

- [ ] **Step 5: Run tests to verify they fail**

Run:

```powershell
dotnet test tests\ULinkRPC.Tests\ULinkRPC.Tests.csproj --no-restore --filter "RpcStatus_Values|ResponseRoundTrip_WithErrorMessage|EncodeResponseError_MatchesWireProtocolV1GoldenBytes|DecodeResponseError_ReadsWireProtocolV1GoldenBytes|HandlerThrows_ReturnsHandlerError|RequestQueueLimit_RejectsRequestsBeyondBudget|CallAsync_HandlerError_ThrowsRpcException"
```

Expected result before production changes: compile fails because `RpcStatus.HandlerError`, `RpcStatus.Overloaded`, `RpcStatus.BadRequest`, and `RpcStatus.ProtocolError` do not exist.

If the command fails earlier with `MSB3491` access denied writing `obj` or `bin`, record that environment limitation and continue only after noting that RED verification is blocked by the local sandbox, not by test logic.

### Task 2: Implement Core and Server Status Mapping

**Files:**
- Modify: `src/ULinkRPC.Core/Protocol/RpcEnvelopes.cs`
- Modify: `src/ULinkRPC.Server/Dispatching/ServerRequestDispatcher.cs`

- [ ] **Step 1: Replace the enum in Core**

In `src/ULinkRPC.Core/Protocol/RpcEnvelopes.cs`, replace the `RpcStatus` enum with:

```csharp
/// <summary>
/// Describes the framework-level outcome of an RPC response.
/// </summary>
public enum RpcStatus : byte
{
    /// <summary>
    /// The request completed successfully and the payload contains the serialized return value.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// The target service or method was not found.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The server handler failed or returned an invalid framework response.
    /// </summary>
    HandlerError = 2,

    /// <summary>
    /// The server could not accept the request because it is overloaded.
    /// </summary>
    Overloaded = 3,

    /// <summary>
    /// The request reached the RPC layer but was invalid for the target RPC contract.
    /// </summary>
    BadRequest = 4,

    /// <summary>
    /// The peer violated the RPC wire protocol or connection state machine.
    /// </summary>
    ProtocolError = 5
}
```

- [ ] **Step 2: Update server dispatcher mapping**

In `src/ULinkRPC.Server/Dispatching/ServerRequestDispatcher.cs`, change `SendOverloadedResponseAsync` to set:

```csharp
Status = RpcStatus.Overloaded,
```

In `DispatchUserHandlerAsync`, change the null response fallback to:

```csharp
Status = RpcStatus.HandlerError,
```

In `DispatchUserHandlerAsync`, change the exception fallback to:

```csharp
Status = RpcStatus.HandlerError,
```

In `DispatchRegistryHandlerAsync`, change the encoded error frame status to:

```csharp
RpcStatus.HandlerError,
```

- [ ] **Step 3: Search for remaining old status references**

Run:

```powershell
rg -n "RpcStatus\.Exception|ReturnsException|ServerError_ThrowsOnClient|status Exception|`Exception`" src tests blog CHANGELOG.md docs
```

Expected result after production changes and before docs cleanup: no `src` or `tests` references to `RpcStatus.Exception`; docs may still contain historical changelog/spec references that Task 3 will address.

- [ ] **Step 4: Run focused tests to verify green**

Run:

```powershell
dotnet test tests\ULinkRPC.Tests\ULinkRPC.Tests.csproj --no-restore --filter "RpcStatus_Values|ResponseRoundTrip_WithErrorMessage|EncodeResponseError_MatchesWireProtocolV1GoldenBytes|DecodeResponseError_ReadsWireProtocolV1GoldenBytes|HandlerThrows_ReturnsHandlerError|RequestQueueLimit_RejectsRequestsBeyondBudget|CallAsync_HandlerError_ThrowsRpcException"
```

Expected result: all selected tests pass.

If blocked by local write permissions to `obj` or `bin`, run `git diff --check` and `rg` checks, then report the test command and exact environment failure in the completion notes.

### Task 3: Update Docs, Versions, and Starter Manifest

**Files:**
- Modify: `blog/content/posts/error-handling.md`
- Modify: `blog/content/posts/api-stability-roadmap.md`
- Modify: `blog/content/reference/api.md`
- Modify: `CHANGELOG.md`
- Modify: `src/ULinkRPC.Core/ULinkRPC.Core.csproj`
- Modify: `src/ULinkRPC.Server/ULinkRPC.Server.csproj`
- Modify: `src/ULinkRPC.Starter/ReleaseVersions.json`
- Modify: `tests/ULinkRPC.Starter.Tests/StarterTemplateGeneratorTests.cs`

- [ ] **Step 1: Update error handling documentation**

In `blog/content/posts/error-handling.md`, update the status list to:

```markdown
- `Ok = 0`: the service method returned successfully. The payload is the return DTO; `void` returns use an empty payload.
- `NotFound = 1`: the server could not find a handler for the requested `serviceId:methodId`.
- `HandlerError = 2`: the server handler failed or returned an invalid framework response.
- `Overloaded = 3`: the server could not accept the request because it is overloaded, such as when a request queue is full.
- `BadRequest = 4`: the request reached the RPC layer but was invalid for the target RPC contract.
- `ProtocolError = 5`: the peer violated the wire protocol or connection state machine.
```

Replace client failure wording with:

```markdown
- A non-`Ok` response throws `RpcException`, with `Status`, `ErrorMessage`, `RequestId`, `ServiceId`, and `MethodId`.
```

- [ ] **Step 2: Update API stability roadmap**

In `blog/content/posts/api-stability-roadmap.md`, replace the stale sentence:

```markdown
Today `RpcStatus` only has `Ok`, `NotFound`, and `Exception`.
```

with:

```markdown
`RpcStatus` is a framework-only status taxonomy: `Ok`, `NotFound`, `HandlerError`, `Overloaded`, `BadRequest`, and `ProtocolError`. Business failures stay in business DTOs.
```

- [ ] **Step 3: Update API reference**

In `blog/content/reference/api.md`, add or update the `RpcStatus` entry so it says:

```markdown
`RpcStatus` describes framework-level RPC response outcomes only. It does not represent business failures. Non-`Ok` responses are surfaced by the client runtime as `RpcException`.
```

Also document that `RpcException.Status` should be used for machine-readable observability and retry decisions.

- [ ] **Step 4: Bump package versions**

In `src/ULinkRPC.Core/ULinkRPC.Core.csproj`, change:

```xml
<Version>0.11.11</Version>
```

to:

```xml
<Version>0.11.12</Version>
```

In `src/ULinkRPC.Server/ULinkRPC.Server.csproj`, change:

```xml
<Version>0.11.10</Version>
```

to:

```xml
<Version>0.11.11</Version>
```

In `src/ULinkRPC.Starter/ReleaseVersions.json`, change:

```json
"Core": "0.11.12",
"Server": "0.11.11",
```

- [ ] **Step 5: Update starter tests**

In `tests/ULinkRPC.Starter.Tests/StarterTemplateGeneratorTests.cs`, update expected package versions:

```csharp
Assert.Equal("0.11.12", GetProjectVersion("src", "ULinkRPC.Core", "ULinkRPC.Core.csproj"));
Assert.Equal("0.11.11", GetProjectVersion("src", "ULinkRPC.Server", "ULinkRPC.Server.csproj"));
```

If the file uses package reference assertions instead of exact project version assertions in the relevant test, update the expected `ULinkRPC.Core` package version to `0.11.12` and `ULinkRPC.Server` to `0.11.11`.

- [ ] **Step 6: Update changelog**

Add a new top entry to `CHANGELOG.md`:

```markdown
## 0.11.12 / 0.11.11

Released: 2026-06-04

Packages:
	- `ULinkRPC.Core` `0.11.12`
	- `ULinkRPC.Server` `0.11.11`

- Replaced the ambiguous `RpcStatus.Exception` with framework-specific statuses: `HandlerError`, `Overloaded`, `BadRequest`, and `ProtocolError`.
- Updated server request dispatch so handler failures return `HandlerError` and request queue saturation returns `Overloaded`.
- Documented that `RpcStatus` is framework-only; business failures belong in business DTOs.
```

- [ ] **Step 7: Run docs/version consistency checks**

Run:

```powershell
rg -n "RpcStatus\.Exception|`Exception = 2`|InvalidOperationException`, with a message containing `RpcStatus`|Today `RpcStatus` only has" src tests blog docs CHANGELOG.md
```

Expected result: no matches except historical design/spec text that intentionally describes the old state.

Run:

```powershell
rg -n "0\.11\.11|0\.11\.10" src\ULinkRPC.Core src\ULinkRPC.Server src\ULinkRPC.Starter tests\ULinkRPC.Starter.Tests CHANGELOG.md
```

Expected result: old versions only appear in historical changelog entries, not current csproj, starter manifest, or active starter test expectations.

### Task 4: Final Verification and Commit

**Files:**
- All files modified by Tasks 1-3.

- [ ] **Step 1: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected result: no output and exit code 0.

- [ ] **Step 2: Run focused test suites**

Run:

```powershell
dotnet test tests\ULinkRPC.Tests\ULinkRPC.Tests.csproj --no-restore --filter "RpcStatus_Values|ResponseRoundTrip_WithErrorMessage|EncodeResponseError_MatchesWireProtocolV1GoldenBytes|DecodeResponseError_ReadsWireProtocolV1GoldenBytes|HandlerThrows_ReturnsHandlerError|RequestQueueLimit_RejectsRequestsBeyondBudget|CallAsync_HandlerError_ThrowsRpcException"
```

Run:

```powershell
dotnet test tests\ULinkRPC.Starter.Tests\ULinkRPC.Starter.Tests.csproj --no-restore --filter "FullyQualifiedName~StarterTemplateGeneratorTests"
```

Expected result: selected tests pass. If local sandbox write permissions block test execution, capture the exact failure and include it in the final report.

- [ ] **Step 3: Review final diff**

Run:

```powershell
git diff --stat
git diff -- src\ULinkRPC.Core\Protocol\RpcEnvelopes.cs src\ULinkRPC.Server\Dispatching\ServerRequestDispatcher.cs tests\ULinkRPC.Tests\RpcServerTests.cs tests\ULinkRPC.Tests\RpcClientTests.cs blog\content\posts\error-handling.md CHANGELOG.md
```

Expected result: the diff only contains the accepted status model, docs, tests, and version metadata.

- [ ] **Step 4: Commit implementation**

Run:

```powershell
git add -A
git commit -m "Implement RPC status error model"
```

Expected result: one commit containing tests, implementation, docs, and version bumps.
