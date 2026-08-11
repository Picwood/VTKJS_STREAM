# ADR 0001: HTTP binary resources and JSON WebSockets

Status: accepted for protocol 1.0.

Use cacheable HTTP requests for raw packed arrays and WebSockets for small commands/events. This keeps resource cancellation, byte ranges, immutable ETags, and browser typed-array handling independent from control-message ordering. Base64 and numeric JSON payloads are forbidden.

SignalR and packed VTK cell resources remain deferred until measurements justify changing the renderer-neutral protocol.
