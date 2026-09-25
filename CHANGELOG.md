# Changelog

All notable changes to this plugin are documented here.

## Unreleased

### Changed

- **Renamed the plugin to "Smarter Collections"** (display name and catalog listing only - a wink
  at [johnpc/jellyfin-plugin-smart-collections](https://github.com/johnpc/jellyfin-plugin-smart-collections),
  since this plugin does the same job: mdblist lists → collections → Jellyseerr requests).
- **MDBList API key is now global**, one key shared by every list, instead of one per list. An
  existing install migrates automatically on first startup after upgrading; the per-list field is
  removed from the config page.

### Added

- **TV show support.** A configured list can now contain both movies and TV shows (MDBList already
  returns both); not-owned shows get a placeholder series (one dummy episode) in a separate TV
  stub library, and pressing Play on one requests the whole series through Jellyseerr (Sonarr
  server/quality profile configurable, separately from Radarr's). "Set up libraries" now creates
  both stub libraries plus Collections in one step.
- **Optional quality version picker.** Add one or more extra Radarr/Sonarr server+profile pairs in
  the config page's Requests section to offer a native Jellyfin version picker (verified live on
  Web and Wolphin) before playback, instead of always requesting with the single default
  profile. Movie/TV stubs switch to a folder-per-title layout with one file per version only when
  at least one extra profile is configured; leaving the list empty keeps today's single-stub
  behavior unchanged.

### Fixed

- **Collections never got an image.** Jellyfin's own image provider for BoxSets only runs on
  locked collections; created collections are now locked (they never carry real online metadata
  anyway, so there's nothing to lose by skipping internet refresh), and existing collections
  self-heal on their next sync.
- **TV/movie stub libraries never got poster/backdrop art** - metadata came through but images
  didn't, because the library's `TypeOptions` set a metadata fetcher without also setting an image
  fetcher.
- Config page: pasting an mdblist.com URL now also fills in Display name (derived from the slug),
  not just username/slug - still editable afterward like the other two fields.

## 0.1.0.0

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
