# MVP scope

## Request configuration and execution

- HTTP methods: GET, POST, PUT, PATCH, DELETE.
- Request inputs: URL, query parameters, headers, JSON body.
- Users can send requests using the selected method and configured values.

## Response inspection

- HTTP status code.
- Elapsed request time.
- Response headers.
- Response body.
- Other diagnostic information available from the request/response, without claiming a definitive cause unsupported by that evidence.

## Reuse

- Request history with the ability to select and resend a previous request using its method, URL, parameters, headers, and body.
- Saved requests organized in collections and available in a later work session.

## Request-safety requirements

- Protect outbound requests against SSRF.
- Enforce request timeouts.
- Limit response size.
- Exact policies, thresholds, and test cases remain open questions; this companion does not prescribe values.
