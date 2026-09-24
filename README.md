# Jellyfin Seasonal Content

A Jellyfin plugin that turns curated [MDBList](https://mdblist.com/) movie lists (for example
"Top 100 Halloween Movies") into native Jellyfin **Collections**, visible on every client — Web,
Android TV (Wolphin), and anywhere else Jellyfin runs. Every configured list shows up as its own
Collection on the **Collections tab**, sitting alongside your other collections.

- **Titles the server already owns play normally.** You get the real library item, never a stub
  duplicate — the plugin checks ownership by TMDb id before ever writing a placeholder.
- **Titles the server doesn't own** appear as lightweight placeholders in the same Collection.
  Pressing Play on one stops playback and submits a request through
  [Jellyseerr](https://github.com/Fallenbagel/jellyseerr), attributed to you, left pending for an
  admin/approver to review and approve.

See `CHANGELOG.md` for what's implemented so far.

## How it works, briefly

Each configured list is fetched from MDBList and split into titles you own and titles you don't.
Not-owned titles get a small `.strm` placeholder file in one shared library folder (one file per
title, shared across every list that includes it) that points at a tiny placeholder video the
plugin itself serves. Both the owned real items and the not-owned placeholders are then added as
members of one native Jellyfin BoxSet per list — that's what shows up on the Collections tab.

## Requirements

- Jellyfin 12.1.0
- An [MDBList](https://mdblist.com/) account and API key, for whichever lists you want to mirror
- A [Jellyseerr](https://github.com/Fallenbagel/jellyseerr) instance, for requesting titles the
  server doesn't own (optional — without it, owned titles and the Collections still work; pressing
  Play on a stub just won't be able to request anything)

## Installation

Not yet packaged for the plugin catalog. For now, build from source and deploy manually: build the
`Jellyfin.Plugin.SeasonalContent` project in Release, copy the resulting DLL into
`/config/data/plugins/SeasonalContent_<version>/` alongside a hand-written `meta.json` (matching
the fields Jellyfin itself writes for other plugins), fix file ownership to match the container's
user, then restart Jellyfin.

## Setup, after installing

1. **Add the stub library.** The plugin's config page (Dashboard → Plugins → Seasonal Content)
   shows the exact path under "Stub library" — add it once as a **Movies**-type library
   (Dashboard → Libraries → Add Media Library), with the TMDb metadata fetcher enabled (the
   default). This is a manual, one-time step; the plugin does not create Jellyfin libraries itself.
   - **Grant access explicitly to every restricted user.** Only users with "Enable access to all
     libraries" see a newly-added library automatically. A user without access sees the
     Collections, but every stub in them will silently appear missing — grant them access to the
     stub library the same way you would any other library.
2. **Set the stub base URL.** This is the `scheme://host:port` that both the server's own
   transcoder process *and every client device on your LAN* can reach to fetch the placeholder
   video. Use a real LAN IP (e.g. `http://192.168.1.10:8096`), not `127.0.0.1` and not a
   reverse-proxied hostname unless you've verified it works from a client device too — `127.0.0.1`
   only resolves for the server itself, and a proxy adds DNS/TLS failure points a plain LAN
   address doesn't have. The config page's "Test stub URL" button proves the *server* can reach
   the URL; it does **not** prove a client device on your LAN can — test that separately (e.g.
   open the URL in a browser on your phone).
3. **Add at least one list** in the config page's Lists section — either paste a full
   `https://mdblist.com/lists/<user>/<slug>` URL (parsed automatically) or fill in the username
   and slug yourself, plus your MDBList API key.
4. *(Optional)* **Configure Jellyseerr** — URL, API key, and optionally a specific Radarr
   server/quality profile to request against. Without this, owned titles and Collections still
   work; only the "press Play to request" flow needs it.
5. Run the "Sync seasonal lists" scheduled task once (Dashboard → Scheduled Tasks), or wait for it
   to run automatically (on startup, then every 6 hours).

## Known limitations (v1)

- **Movies only.** No TV show support yet — a show's stub would need season/episode structure and
  duplicate detection is more involved; out of scope for v1.
- **No home-page row.** Jellyfin core has no way to pin a Collection to the home screen; only the
  Collections tab.
- **An owned title with no TMDb id** (a bad metadata match, or a manually-added file) will still
  get stubbed as a duplicate — the ownership check keys entirely on TMDb id.
- **Stubs appear in search and in "Latest" for the stub library**, the same as any other library
  item would. There's no way to exclude them from those views without also hiding real content.
- **No 4K-specific request flow yet** — a request uses the single configured Radarr
  server/profile, if any.

## Credit

Architecturally inspired by [luall0/jellynext](https://github.com/luall0/jellynext)'s STRM
virtual-library approach for surfacing non-owned titles across clients. This plugin is an
independent, clean-room reimplementation — no jellynext code is reused: jellynext was read only to
understand its behavior, and this plugin's API signatures and shapes were verified independently
against the official Jellyfin plugin template, Jellyfin's own NuGet packages, and the real running
server, never copied from jellynext's source.

Collection grouping was also informed by evaluating
[johnpc/jellyfin-plugin-smart-collections](https://github.com/johnpc/jellyfin-plugin-smart-collections)
as an alternative to building BoxSets directly; its tag-driven rule approach shaped the comparison
in `docs/implementation-plan.md` §6, though this plugin ultimately creates BoxSets itself via
`ICollectionManager` rather than depending on it. No code from that plugin is reused.

## License

GPL-3.0. See `LICENSE`.
