# Changelog

All notable changes to this plugin are documented here.

## Unreleased

Not yet released to the plugin catalog. Everything below has been built and live-verified against
a real Jellyfin 12.1.0 server, but the version number stays `0.1.0.0` until the remaining
release-prep steps (a tagged GitHub release, a hosted plugin repository manifest, a catalog
install test) are complete.

### Added

- Curated MDBList lists as native Jellyfin Collections, one BoxSet per configured list, shared
  across lists via one stub folder keyed by TMDb id.
- Ownership check: a title the server already owns is added to the Collection as the real item,
  never stubbed.
- Scheduled "Sync seasonal lists" task (stub reconcile → library scan → collection reconcile),
  triggers on startup and every 6 hours, with per-list failure isolation.
- Playback interception: pressing Play on a stub stops playback and requests it through
  Jellyseerr, attributed to the playing user, left pending for approval.
- Full config page: connection settings (stub base URL, Jellyseerr URL/key) with test-connection
  buttons, list management (add/remove, paste-a-URL parsing), cascading Radarr server/profile
  dropdowns, and sync options.

### Known limitations

See the README's "Known limitations" section.
