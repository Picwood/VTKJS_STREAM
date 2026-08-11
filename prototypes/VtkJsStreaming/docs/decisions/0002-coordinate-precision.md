# ADR 0002: origin-relative float32 render coordinates

Status: accepted for protocol 1.0.

Transmit one float64 world origin in scene metadata and float32 position offsets in binary resources. The browser renders the offsets and reconstructs CPU world coordinates for picks. This preserves local detail near large engineering coordinates without doubling the principal geometry resource.
