# Awards Metadata for Jellyfin

[![Build](https://img.shields.io/github/actions/workflow/status/scottfridwin/jellyfin-plugin-awards-metadata/build.yaml?branch=main&label=build)](https://github.com/scottfridwin/jellyfin-plugin-awards-metadata/actions/workflows/build.yaml)
[![Release](https://img.shields.io/github/v/release/scottfridwin/jellyfin-plugin-awards-metadata)](https://github.com/scottfridwin/jellyfin-plugin-awards-metadata/releases/latest)
[![Jellyfin](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fscottfridwin%2Fjellyfin-plugin-awards-metadata%2Fmain%2Fbuild.yaml&query=%24.targetAbi&label=Jellyfin&logo=jellyfin&color=00a4dc)](https://jellyfin.org/)
[![Downloads](https://img.shields.io/github/downloads/scottfridwin/jellyfin-plugin-awards-metadata/total)](https://github.com/scottfridwin/jellyfin-plugin-awards-metadata/releases)
[![License](https://img.shields.io/github/license/scottfridwin/jellyfin-plugin-awards-metadata)](LICENSE)

Tag the movies in your [Jellyfin](https://jellyfin.org/) library with the awards they won or were nominated for, such as `award-academy-awards-winner-best-picture-2014`. Award data comes from the public award pages on [TMDB](https://www.themoviedb.org/award); no API key is needed.

> [!NOTE]
> **AI disclosure:** This project is built and maintained with substantial help from AI coding assistants (GitHub Copilot and Claude). AI is used to write and modify the code, tests, documentation and CI configuration, and to manage the repository. Dependency updates are merged and released automatically, without human review, when the automated tests pass. Review the code and test it in your own environment before relying on it.

## Features

- **Any award TMDB lists** — Academy Awards, Golden Globes, BAFTA, Cannes and many more; pick the ones you want
- **Winners and nominees**, each optional
- **Your tag format** — build tags from the organization, result, category and year
- **Leaves your own tags alone** — the plugin only changes tags it created, and cleans them up when the format or selection changes
- **Runs on a schedule** — refreshes award data weekly and tags new movies daily
- **Polite scraping** — rate limiting, retries and back-off when talking to TMDB

## Requirements

- Jellyfin — the badge above shows the version the latest release targets; the plugin catalog automatically offers the newest release that is compatible with your server
- Movies identified with a TMDB ID (Jellyfin's default metadata providers set this)

## Installation

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add:

   ```text
   https://scottfridwin.github.io/jellyfin-plugin-awards-metadata/manifest.json
   ```

2. Open **Dashboard → Plugins → Catalog**, install **Awards Metadata**, and restart Jellyfin.

<details>
<summary>Manual installation</summary>

Download the zip for your Jellyfin version from [Releases](https://github.com/scottfridwin/jellyfin-plugin-awards-metadata/releases), extract it to `<jellyfin-data>/plugins/Jellyfin.Plugin.AwardsMetadata/`, and restart Jellyfin. The repository method above is preferred because Jellyfin then installs updates for you.

</details>

## Getting started

1. Open **Dashboard → Plugins → Awards Metadata**.
2. Click **Discover Organizations** to load the list of awards from TMDB.
3. Tick the organizations you want (for example *Academy Awards* and *Golden Globes*) and click **Save**.
4. Open **Dashboard → Scheduled Tasks** and run **Scrape Awards Data**, then **Apply Award Tags**.

The tags appear on your movies and can be used anywhere Jellyfin supports tags, for example the **Tags** filter in a movie library or tag rules in collection and playlist plugins. After the first run, both tasks keep things up to date on their own.

## Configuration

| Setting | Description | Default |
| --- | --- | --- |
| Rate Limit Delay (ms) | Pause between requests to TMDB (100–60000) | `1000` |
| Max Retry Count | Retries for a failed request (0–10) | `3` |
| Request Timeout (seconds) | Timeout for each request (5–120) | `30` |
| Enable Winner Tagging | Tag movies that won | on |
| Enable Nominee Tagging | Tag movies that were nominated but did not win | on |
| Tag Format | Template for each tag (see below) | `award-{awardType}-{awardResult}-{awardCategory}-{awardYear}` |
| Award Organizations | Which awards to scrape and tag | *none* |

### Tag format

| Placeholder | Value | Example |
| --- | --- | --- |
| `{awardType}` | Organization | `academy-awards` |
| `{awardResult}` | `winner` or `nominee` | `winner` |
| `{awardCategory}` | Category | `best-picture` |
| `{awardYear}` | Ceremony year | `2014` |

Names are lower-cased, accents are removed, and anything other than letters and digits becomes a hyphen.

| Format | Example tag |
| --- | --- |
| `award-{awardType}-{awardResult}-{awardCategory}-{awardYear}` (default) | `award-academy-awards-winner-best-picture-2014` |
| `{awardType}-{awardResult}` | `academy-awards-winner` |
| `{awardType}-{awardYear}` | `golden-globes-2023` |

Duplicate tags are merged, so short formats give one tag per movie rather than one per category.

## Scheduled tasks

| Task | What it does | Default schedule |
| --- | --- | --- |
| Scrape Awards Data | Downloads award results from TMDB for the selected organizations | Sundays at 3:00 |
| Apply Award Tags | Adds, updates and removes award tags on your movies | Daily at 4:00 |
| Remove Existing Award Tags | Removes every tag this plugin has added | Manual |

To remove the tags for good, untick the organizations (or disable winner and nominee tagging) before running **Remove Existing Award Tags**; otherwise the next **Apply Award Tags** run adds them back.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| No tags are applied | At least one organization is selected and saved; **Scrape Awards Data** completed (see **Dashboard → Logs**); the movie has a TMDB ID. |
| Tags disappeared after changing settings | Expected: old tags are removed and new ones are added on the next **Apply Award Tags** run. |
| Scrape fails with timeouts | Increase **Request Timeout**. If TMDB is throttling requests, also increase **Rate Limit Delay**. |
| An award or category is missing | The plugin can only tag what TMDB lists; check the organization's page on [TMDB](https://www.themoviedb.org/award). |

## Further reading

- [How it works](docs/how-it-works.md)
- [Development](docs/development.md)

## License

[GPL-3.0](LICENSE)
