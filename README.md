# Jellyfin Plugin: Awards Metadata

Automatically tag movies in your [Jellyfin](https://jellyfin.org/) library with award nominations and wins scraped from [TMDB](https://www.themoviedb.org/).

## Features

- **Award discovery** — dynamically discovers all award organizations available on TMDB (Academy Awards, Golden Globes, BAFTA, etc.)
- **Full ceremony scraping** — downloads and parses complete ceremony data including categories, nominees, and winners
- **Configurable tagging** — apply tags for winners, nominees, or both using a customizable tag format
- **Managed tags** — only modifies tags it owns; never touches user-created tags
- **Safe updates** — changing tag format or configuration cleanly removes old tags and applies new ones
- **Rate limiting & retries** — respects TMDB rate limits with configurable delays, exponential backoff, and Retry-After support
- **Idempotent operations** — running any task multiple times produces identical results without duplicates
- **Scheduled tasks** — scrape data, apply tags, and remove stale tags on configurable schedules
- **Extensible architecture** — scraper library is decoupled from the plugin, enabling future providers and storage backends

## How It Works

1. **Discover** — Query TMDB for available award organizations
2. **Configure** — Select which organizations to track in the plugin settings
3. **Scrape** — Download and parse ceremony pages into a local awards database
4. **Apply** — Match movies in your library by TMDB ID and apply tag metadata

### Tag Format

Tags are generated using a configurable template with placeholders:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{awardType}` | Organization name (slugified) | `academy-awards` |
| `{awardResult}` | Win or nomination | `winner` / `nominee` |
| `{awardCategory}` | Category name (slugified) | `best-picture` |
| `{awardYear}` | Ceremony year | `2024` |

**Default format:** `award-{awardType}-{awardResult}-{awardCategory}-{awardYear}`

**Example tags:**
- `award-academy-awards-winner-best-picture-2024`
- `award-golden-globes-nominee-best-director-2023`

**Simplified format:** `{awardType}-{awardResult}` → `academy-awards-winner`

## Installation

### Plugin Repository (Recommended)

1. Go to **Dashboard → Plugins → Repositories**
2. Click **+** to add a new repository
3. Enter the repository URL:
   ```
   https://scottfridwin.github.io/jellyfin-plugin-awards-metadata/manifest.json
   ```
4. Go to **Dashboard → Plugins → Catalog**
5. Search for **Awards Metadata** and click **Install**
6. Restart Jellyfin

> **Dev/testing builds:** To test pre-release builds, use this repository URL instead:
> ```
> https://scottfridwin.github.io/jellyfin-plugin-awards-metadata/dev/manifest.json
> ```
> Dev builds are updated on every push to the `dev` branch and may be unstable.

### Manual Installation

1. Download the latest release from [GitHub Releases](https://github.com/scottfridwin/jellyfin-plugin-awards-metadata/releases)
2. Extract the zip file into your Jellyfin plugins directory:
   ```
   <jellyfin-data>/plugins/Jellyfin.Plugin.AwardsMetadata/
   ```
   The folder should contain:
   - `Jellyfin.Plugin.AwardsMetadata.dll`
   - `Jellyfin.Plugin.AwardsMetadata.Scraper.dll`
   - `HtmlAgilityPack.dll`
3. Restart Jellyfin

## Configuration

After installation, go to **Dashboard → Plugins → Awards Metadata** and configure:

| Setting | Description | Default |
|---------|-------------|---------|
| **Rate Limit Delay (ms)** | Minimum delay between HTTP requests to TMDB | `1000` |
| **Max Retry Count** | Maximum retry attempts for failed requests | `3` |
| **Request Timeout (s)** | HTTP request timeout | `30` |
| **Enable Winner Tagging** | Generate tags for award winners | `true` |
| **Enable Nominee Tagging** | Generate tags for nominees (non-winners) | `true` |
| **Tag Format** | Template for generated tag strings | `award-{awardType}-{awardResult}-{awardCategory}-{awardYear}` |

### Getting Started

1. Install the plugin and restart Jellyfin
2. Go to **Dashboard → Plugins → Awards Metadata**
3. Click **Discover Organizations** to query TMDB for available award types
4. Check the organizations you want to track (e.g., Academy Awards, Golden Globes)
5. Click **Save**
6. Run the **Scrape Awards Data** scheduled task (Dashboard → Scheduled Tasks)
7. Run the **Apply Award Tags** scheduled task

After initial setup, both tasks run automatically on schedule.

## Scheduled Tasks

| Task | Description | Default Schedule |
|------|-------------|-----------------|
| **Scrape Awards Data** | Downloads and parses award data from TMDB for enabled organizations | Weekly (Sunday 3:00 AM) |
| **Apply Award Tags** | Matches library movies by TMDB ID and applies/updates tags | Daily (4:00 AM) |
| **Remove Stale Award Tags** | Removes all managed tags (useful for cleanup or reconfiguration) | Manual only |

## Architecture

The solution is split into two projects:

```
Jellyfin.Plugin.AwardsMetadata         → Jellyfin plugin (config, tasks, API, tag generation)
Jellyfin.Plugin.AwardsMetadata.Scraper → Reusable library (downloading, parsing, persistence)
```

The scraper library has no dependency on Jellyfin and owns the canonical awards data model:

```
Downloader → HTML → Parser → Award Models → Persistence → Consumer
```

Each component is individually testable with saved HTML fixtures.

## Development

### Prerequisites

- .NET 9.0 SDK

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
```

### Publish (plugin DLLs only)

```bash
dotnet publish Jellyfin.Plugin.AwardsMetadata -c Release -o artifacts
```

### Dev Container

Open in VS Code with the Dev Containers extension for a pre-configured development environment.

## Troubleshooting

### No tags are being applied

1. Check that you've run **Discover Organizations** and selected at least one organization
2. Verify the **Scrape Awards Data** task has completed successfully (check Dashboard → Logs)
3. Confirm your movies have TMDB provider IDs (movies without TMDB IDs are skipped)
4. Enable Debug logging to see per-movie processing details

### Tags disappeared after a configuration change

This is expected behavior. When the tag format changes, old managed tags are removed and new ones are applied on the next **Apply Award Tags** run.

### Scrape fails with timeout errors

Increase the **Request Timeout** setting and reduce the **Rate Limit Delay** if your connection to TMDB is slow.

## License

GPL-3.0 — see [LICENSE](LICENSE) for details.
