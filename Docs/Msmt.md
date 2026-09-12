# Mercury Secure Message Transport (MSMT)

MSMT is an open, standards-based interface for secure message transport over IP networks, defined by MITRE's *Mercury Secure Message Transport Interface Control Document (ICD)*, currently at version 1.2. It was created to move legacy military messaging systems — historically built around serial, point-to-point terrestrial links — onto modern IP networks without losing the message-level integrity and authenticity guarantees those legacy systems relied on. The interface is presented in the context of Allied Communications Publication 128 (ACP 128) format messaging, but it is payload-agnostic and equally usable for other message formats.

MSMT is **not a network protocol in its own right** — it is a specific, fixed configuration of TLS 1.3, paired with a thin, standardized library API. Responsibility for interpreting message content stays entirely with the application; MSMT treats every message and acknowledgement as an opaque, variable-length sequence of bytes.

This document summarizes the standard for reference purposes. It does not describe any particular implementation's code, classes, or function signatures.

## Motivation

- **Insider threat and eavesdropping** — earlier IP messaging approaches (e.g. Virtual Circuit Protocol) do not encrypt messages or guarantee end-to-end integrity, leaving traffic on the "red side" of existing Type-1 encrypted WAN links unprotected from tampering and spoofing by anyone with access to that segment.
- **Lack of a common standard** — many interim, IP-based messaging solutions exist across the messaging community, but none had emerged as an open, non-proprietary standard incorporating modern security practices.
- **Long-term maintainability** — standardizing the interface (rather than a specific implementation) allows cryptographic algorithms to be patched or upgraded uniformly, including a future migration to post-quantum algorithms, with minimal impact on the applications built against it.

## Core Guarantees

An application built against the MSMT interface gets, from the transport alone:

- **Confidentiality and integrity** of every message in transit, end-to-end, above and beyond whatever protection an underlying WAN link already provides.
- **Mutual peer authentication**, so a receiver has cryptographic assurance that a message actually originated from the claimed sender, and vice versa.
- **Network interoperability** — any two implementations that correctly follow the ICD can communicate, regardless of language or vendor.
- **Full application control** over message parsing, formatting, and acknowledgement semantics — MSMT never inspects or interprets message content.

## Transport Foundation

MSMT is built directly on two well-established layers:

- **TCP** provides the connection-oriented, reliable, in-order byte stream.
- **TLS 1.3** runs over the TCP connection and supplies peer authentication, data integrity, and confidentiality. MSMT mandates TLS 1.3 specifically (never negotiating down to 1.2 or earlier) because 1.3 removed an entire class of record-layer vulnerabilities present in earlier TLS versions, restricted symmetric encryption to authenticated encryption with associated data (AEAD) modes, and added key agreement algorithms with perfect forward secrecy — meaning a compromised long-term private key cannot be used to decrypt previously recorded sessions.

To keep implementations interoperable and easy to reason about from a security standpoint, the ICD pins TLS down to a narrow, well-understood configuration rather than leaving it fully general-purpose:

- Only two cipher suites are permitted (`TLS_CHACHA20_POLY1305_SHA256` and `TLS_AES_256_GCM_SHA384`), offered by the client in that fixed order.
- The client's advertised TLS version list contains only TLS 1.3 — a server must reject any attempt to negotiate a lesser version.
- Peer identity can be established either through public-key certificates (PKI) or pre-shared keys (PSK), for environments where an enterprise PKI isn't available. Certificate chains that are expired, revoked, use invalid key lengths/signature algorithms, or don't match their claimed private key are rejected outright.
- Clients must present a Server Name Indication (SNI) extension with a fully qualified hostname in every handshake, and servers must abort connections that omit it or supply an invalid hostname — necessary so a server reachable under more than one DNS hostname can select the correct identity and credentials, but required unconditionally regardless of how many hostnames a given server actually serves.

## Modes of Operation

MSMT supports three modes, chosen based on how a deployment wants to trade network/latency overhead against exposure of any single TLS connection:

| Mode | Connection lifetime | Notes |
|------|---------------------|-------|
| **Message Mode** (default) | One TLS connection per message | Maximizes security by minimizing how much traffic — and how much time — any single TLS session is exposed for, at the cost of a full TLS handshake per message. Applications are encouraged to bundle multiple small application-level messages (e.g. several ACP 128 messages) into a single MSMT message to amortize this cost. |
| **Message Mode with Rekeying** | One TLS connection across several messages, periodically rekeyed | Avoids a full connection teardown/handshake between every message by triggering a TLS rekey (a fresh set of traffic encryption keys within the same connection) instead. The number of rekeys allowed before a full reset is configurable per deployment. |
| **Session Mode** | One TLS connection across many messages, up to a negotiated lifetime | Intended for constrained or unreliable links where per-message handshake overhead is unacceptable. The client and server negotiate a maximum connection lifetime immediately after the TLS handshake completes; the connection is used for an arbitrary number of messages until that lifetime expires, at which point a new connection must be negotiated. Idle Session Mode connections are kept alive with periodic keep-alive messages (randomized between three and five minutes by default) so they aren't dropped for inactivity. |

Aside from connection lifecycle, the sequence of interactions between a client and server application is the same across all three modes — once a connection is established, the modes are transparent to the messaging application.

## General API Flow

The interface specification describes a consistent lifecycle for both message sender ("client") and receiver ("server") roles, regardless of mode:

1. **Initialize secure transport** — both sides supply the library with what it needs to establish TLS: certificate/key locations or pre-shared keys, and (for a client) the destination address, or (for a server) the local listening interface. A single node can act as a client to multiple destinations, or a server across multiple listening interfaces, simultaneously.
2. **Server start** — the server begins listening for inbound TLS connections on a well-known port. This is a non-blocking, asynchronous operation from the application's perspective.
3. **Send** — at some point after initialization, the client hands a message (an opaque byte sequence) to the library. Depending on the configured mode, this either reuses an existing connection (rekeying or reusing a session as appropriate) or establishes a brand-new TLS connection. The send operation is blocking: it returns only once the message has been fully delivered and acknowledged by the remote peer, or an error/timeout has occurred, and applications are expected to always check its result.
4. **Connection handling** — each inbound client connection on the server side is handled independently (conceptually on its own thread), transparent to the application.
5. **Message delivery callback** — once a full message has been received and decrypted, the server-side library invokes an application-supplied callback with the message content. Numerous additional logging callbacks are available for auditing and troubleshooting, invoked at other points in the transmission/reception lifecycle.
6. **Acknowledgement** — after the receiving application validates the message, it tells the library whether to acknowledge (ACK) or negatively acknowledge (NACK) it. This determination is entirely up to the application; MSMT does not interpret the message to decide validity itself. The client indicates whether it wants an acknowledgement at all when it sends; the server's acknowledgement flag mirrors that.
7. **Connection teardown (Message Mode) or continuation (Session Mode)** — in the default mode, the connection is torn down immediately following the acknowledgement. In Session Mode, the same connection instead remains open for further messages until its negotiated lifetime is reached.
8. **Return status** — the original blocking send call on the client returns success (an ACK was received) or failure (a NACK, an error, or no response at all — in which case a null/absent result is returned to the caller and it is up to the application how to proceed, e.g. retry or escalate).

A **reachability check** is available as a lighter-weight alternative to a real send: the client goes through the full process of establishing a connection and sending a specially-flagged test message, and the server echoes it back and tears the connection down, without ever handing the test message to the server's application logic. This gives a "ping"-style way to verify a peer is reachable and correctly configured without generating any real message traffic.

## On-Wire Message Structure (Conceptual)

Every message and acknowledgement MSMT sends over an established TLS connection is prefixed with a small fixed-size header before the opaque message payload. Conceptually, the header conveys:

- **API/feature version** — identifies which revision of the header format and feature set the sender is using, so a receiver knows how to interpret the rest of the header and rejects anything it doesn't understand (closing the connection and returning an error rather than guessing).
- **RSV** — one byte immediately following the version, whose meaning depends on the API version: versions 1 and 3 require it to be zero and don't expose it to applications, while version 2 exposes it for application-defined custom usage. At API version 3 — the version this document otherwise describes — it must always be zero.
- **Flags** — a set of single-bit indicators carried alongside every message, covering: whether the message was valid (set by the receiver), whether an acknowledgement was requested/given, whether this is a reachability check rather than a real message, whether the message was malformed, and whether this exchange is negotiating a Session Mode connection's lifetime. Any flag bits not defined by the current version must be zero, and a receiver must reject a message that sets an unrecognized flag.
- **Message ID** — a randomly generated identifier unique to each message, echoed back in its corresponding acknowledgement so the sender can correlate the two and so logging/forensics tooling can trace a message's full round trip. This field was introduced in version 1.2 specifically to support that tracking, and its addition changed the header's size/layout relative to versions 1.0/1.1 — meaning 1.2 is **not wire-compatible** with earlier MSMT versions.
- **Message length** — the length, in bytes, of the payload that follows (the application message or acknowledgement itself).

Beyond this header, MSMT does not touch the payload in any way — it is handed to and from the application exactly as supplied.

## Operational Risks and Mitigations

- **TLS session setup failure** — because most of TLS's complexity is hidden behind the library, a message originator has limited visibility into *why* a handshake failed (expired certificates, explicit network blocking, routing loops, a crashed destination, etc.). MSMT cannot resolve most of these root causes itself, but its blocking send/reachability-check operations give applications a way to detect and react to a failure rather than silently losing a message.
- **Incompatible inspection middleboxes** — because MSMT enforces TLS 1.3 exclusively with no fallback, any network element that performs SSL/TLS inspection but doesn't support 1.3 will be incompatible with MSMT traffic passing through it.
- **Version compatibility** — as noted above, the version 1.2 header change means 1.2 endpoints cannot interoperate with 1.0/1.1 endpoints. Beyond header versioning, operators are also responsible for keeping deployment-level configuration (such as shared pre-shared keys or the chosen mode of operation) consistent across the client and server population.

## Availability

MSMT is distributed as reference libraries (C++ and Java) that implement the ICD, built on top of standard, freely available cryptographic libraries (OpenSSL for C++, the JDK's built-in TLS support for Java). Any independent implementation that correctly follows the ICD is expected to interoperate with these reference libraries and with each other, since the wire format and TLS configuration — not any particular library's internals — are what the ICD actually standardizes.

The full *Mercury Secure Message Transport Interface Control Document* (v1.2) is available at [`Msmt/ICD.pdf`](../Msmt/ICD.pdf), with a Markdown transcription at [`Msmt/ICD.md`](../Msmt/ICD.md).
