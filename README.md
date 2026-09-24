# Jellyfin Seasonal Content

A Jellyfin plugin that turns curated [MDBList](https://mdblist.com/) movie lists (for example
"Top 100 Halloween Movies") into native Jellyfin Collections, visible on every client. Titles
the server already owns play normally; titles it doesn't own can be requested through
[Jellyseerr](https://github.com/Fallenbagel/jellyseerr) by pressing Play.

**Status: early development, not usable yet.** See `docs/status.md` for what's implemented and
`docs/implementation-plan.md` for the full design and milestone plan.

## Credit

Architecturally inspired by [luall0/jellynext](https://github.com/luall0/jellynext)'s STRM
virtual-library approach for surfacing non-owned titles across clients. This plugin is an
independent, clean-room reimplementation — no jellynext code is reused. See
`docs/implementation-plan.md` §0 for why.

## Requirements

- Jellyfin 12.1.0
- An MDBList account and API key
- A Jellyseerr instance (for requesting titles the server doesn't own)

## Installation

Not yet packaged for the plugin catalog (see `docs/implementation-plan.md` M7). For now, build
from source and deploy manually — see `scripts/deploy.sh`.

## License

GPL-3.0. See `LICENSE` and `docs/decisions.md` for why.
