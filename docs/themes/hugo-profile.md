# Hugo Profile theme inventory

Practical inventory of [gurusabarish/hugo-profile](https://github.com/gurusabarish/hugo-profile) for Hugo Matters CMS and theme-pack work.

| Item | Value |
|------|--------|
| Upstream | https://github.com/gurusabarish/hugo-profile |
| Demo | https://hugo-profile.netlify.app |
| Wiki | https://github.com/gurusabarish/hugo-profile/wiki |
| Inventory snapshot | theme commit `ecc48c8` (2026-02-08, master) |
| Hugo Matters pack | `src/HugoMatters.ThemePacks/Packs/hugo-profile.json` |

**Do not invent fields.** Everything below is verified against theme layouts and `exampleSite/`.

---

## Architecture summary

Hugo Profile is a **params-first portfolio + blog** theme.

| Concern | Where it lives | Consumed by |
|---------|----------------|-------------|
| Homepage sections (hero, about, experience, education, projects, achievements, contact) | `params.*` in site config (`hugo.yaml` / `hugo.toml`) | `layouts/index.html` → `layouts/partials/sections/*` |
| Blog / list posts | `content/blogs/` (example convention) | `_default/list.html`, `_default/single.html` |
| Gallery page | Content page with `layout: gallery` | `_default/gallery.html` |
| Optional project pages | Content with `type: projects` | `layouts/projects/list.html` + home `projects.html` |
| About-style single page | Content using about layout | `_default/about.html` |
| Menus | `menu.main` / language menus | `partials/sections/header.html` |
| i18n strings | `i18n/*.toml` + optional `languages.*.params` overrides | Partials / nav labels |

Homepage render order (`layouts/index.html`):

1. hero  
2. about  
3. experience  
4. education  
5. projects  
6. achievements  
7. contact  

Each section is gated by `params.<section>.enable` (default `false` if unset).

Language-aware sections prefer `.Site.Language.Params.<section>` and fall back to `.Site.Params.<section>`.

---

## Content types and front matter

### Archetype (default)

Source: `archetypes/default.md`

| Field | Notes |
|-------|--------|
| `title` | string |
| `date` | datetime |
| `draft` | bool |
| `author` | optional |
| `tags` | list |
| `image` | optional featured image path/URL |
| `description` | optional |
| `toc` | optional (TOC sidebar on singles) |

### Blog posts (`content/blogs/`)

Example: `exampleSite/content/blogs/*.md`, section index `_index.md` (`title` only).

| Field | Required | Used by | Notes |
|-------|----------|---------|--------|
| `title` | yes | list/single | |
| `date` | yes | list/single | |
| `draft` | no | Hugo | |
| `author` | no | `single.html` | Shown above date |
| `tags` | no | `single.html` sidebar | Links to `tags/<tag>` |
| `image` | no | list cards, single featured, footer recent | |
| `description` | no | meta / social share text | |
| `toc` | no | `single.html` | Default true if omitted in template check |
| `mathjax` | no | `partials/scripts.html` | Per-page MathJax; also `params.mathjax` site-wide |
| `github_link` | example only | not referenced in layouts checked | Present in example posts; safe to ignore for CMS unless you add custom partials |
| `socialShare` | no | `single.html` | Overrides `params.singlePages.socialShare` |
| `enableReadingTime` | no | `single.html` | Forces reading-time UI |

Taxonomies: **tags** (and Hugo categories if you add them). Example posts use tags heavily.

**CMS note:** The built-in Hugo Matters pack currently sets `postsDirectory` to `content/posts`, but the upstream example uses **`content/blogs`**. Prefer aligning the pack / test site with `content/blogs` (and `params.footer.recentPosts.path: "blogs"`).

### Gallery page

Example: `exampleSite/content/gallery.md`  
Layout: `_default/gallery.html`

| Field | Required | Notes |
|-------|----------|--------|
| `title` | yes | |
| `date` | no | |
| `draft` | no | |
| `description` | no | Emojified subtitle |
| `layout` | **yes** for gallery | Must be `"gallery"` |
| `galleryImages` | yes for images | List of `{ src: <url-or-path> }` |
| `viewer` | no | Default true — enables Viewer.js |
| `viewerOptions` | no | Passed into Viewer.js init (example uses YAML map-like syntax) |

Body markdown is rendered above the image grid.

### Project content pages (optional)

Home projects section also ranges `where .Site.RegularPages "Type" "projects"`.

| Field | Notes |
|-------|--------|
| `title` | page title |
| `image` | card image |
| `badges` | string list |
| `links` | `{ icon, url }` list (Font Awesome class on `icon`) |
| `showInHome` | bool, default `true` — hide from homepage grid if false |
| body | used as summary on home / list |

List layout: `layouts/projects/list.html` (paginated).

### About layout pages

Layout: `_default/about.html` (use when a content page needs the about chrome).

| Field | Notes |
|-------|--------|
| `title` | |
| `description` | meta description |
| `image` | sidebar portrait |
| `name` | displayed under image |
| `socialLinks.fontAwesomeIcons` | `{ icon, url }` |
| `socialLinks.customIcons` | `{ icon, url }` (`icon` is image path) |
| body | main article |

This is separate from the **homepage** About section (`params.about`).

### Shortcodes

Only one theme shortcode: `layouts/shortcodes/dynamic-img.html`.

| Param | Default | Notes |
|-------|---------|--------|
| `src` | required | Path segment appended to Cloudinary URL |
| `title` | | alt/title |
| `width` | `w_auto` | Cloudinary width transform |
| `style` | `max-width:80%` | inline style |

Requires `params.cloudinary_cloud_name`. Not needed for a basic demo site.

---

## Homepage / site params (full shapes)

Canonical English shapes from `exampleSite/hugo.yaml` `params:` and language overrides under `languages.es|fr.params`.

### Global / chrome

| Param path | Purpose |
|------------|---------|
| `params.title` | Brand / profile name |
| `params.description` | Site description / meta |
| `params.favicon` | Favicon path |
| `params.staticPath` | Optional prefix for static asset URLs |
| `params.useBootstrapCDN` | `true` / `"css"` / `"js"` / other |
| `params.cloudinary_cloud_name` | For `dynamic-img` shortcode |
| `params.mathjax` | Site-wide MathJax |
| `params.animate` | Homepage fade animations |
| `params.theme.disableThemeToggle` | |
| `params.theme.defaultTheme` | `"light"` / `"dark"` |
| `params.font.*` | `fontSize`, `fontWeight`, `lineHeight`, `textAlign` |
| `params.color.*` / `params.color.darkmode.*` | Colour tokens (see wiki) |
| `params.customScripts` | HTML injected before `</body>` |

### Navbar (`params.navbar`)

| Field | Notes |
|-------|--------|
| `align` | `ms-auto` / `mx-auto` / `me-auto` |
| `brandLogo` | optional; defaults to favicon |
| `showBrandLogo` | default true |
| `brandName` | default site title |
| `disableSearch` | |
| `searchPlaceholder` | |
| `stickyNavBar.enable` | |
| `stickyNavBar.showOnScrollUp` | |
| `enableSeparator` | |
| `menus.disableAbout` … `disableContact` | Hide auto section links |

### Hero (`params.hero`) — partial `sections/hero/index.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `intro`, `title`, `subtitle`, `content` | `content` markdownified |
| `image` | |
| `roundImage` | circular image |
| `bottomImage.enable` | decorative SVG |
| `button.enable`, `button.name`, `button.url`, `button.download`, `button.newPage` | |
| `socialLinks.fontAwesomeIcons[]` | `{ icon, url }` |
| `socialLinks.customIcons[]` | `{ icon, url }` |

### About (`params.about`) — `sections/about.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `title` | |
| `image` | portrait (read from `.Site.Params.about.image`) |
| `content` | markdown |
| `skills.enable` | |
| `skills.title` | |
| `skills.items[]` | strings (markdownified) |

### Experience (`params.experience`) — `sections/experience.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `title` | optional override of i18n |
| `items[]` | companies |

**Company item:**

| Field | Notes |
|-------|--------|
| `company` | tab label |
| `companyUrl` | link on first job row |
| `jobs[]` | roles at that company |

**Job:**

| Field | Notes |
|-------|--------|
| `name` | role title |
| `date` | display string (e.g. `Jan 2023 - present`) |
| `content` | markdown body |
| `info.content` | optional tooltip |
| `featuredItems.fontAwesomeIcons[]` | `{ icon, url, tooltip? }` |
| `featuredItems.customIcons[]` | `{ icon, url, tooltip? }` |

### Education (`params.education`) — `sections/education.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `title` | optional |
| `index` | bool — show numbered index column |
| `items[]` | |

**Education item:**

| Field | Notes |
|-------|--------|
| `title` | degree / programme |
| `school.name` | |
| `school.url` | optional |
| `date` | display string |
| `GPA` | display string |
| `content` | markdown |
| `featuredLink.enable` | |
| `featuredLink.name` | button label (default `"Featured"`) |
| `featuredLink.url` | |

### Projects (`params.projects`) — `sections/projects.html`

Config-driven cards **plus** optional `type: projects` pages.

**Config item:**

| Field | Notes |
|-------|--------|
| `title` | |
| `content` | plain/HTML text in card (not markdownified in template) |
| `image` | |
| `badges[]` | strings |
| `links[]` | `{ icon, url }` |
| `featured.name` / `featured.link` | CTA button |

### Achievements (`params.achievements`) — `sections/achievements.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `title` | optional |
| `items[]` | |

**Item:**

| Field | Notes |
|-------|--------|
| `title` | |
| `content` | plain text (not markdownified) |
| `url` | optional — wraps card in `<a>` |
| `image` | optional |

### Contact (`params.contact`) — `sections/contact.html`

| Field | Notes |
|-------|--------|
| `enable` | |
| `title` | optional |
| `content` | markdown + emoji |
| `btnName` | |
| `btnLink` | preferred CTA href |
| `email` | legacy mailto if no `btnLink` |
| `formspree.enable` | |
| `formspree.formId` | |
| `formspree.emailCaption` | |
| `formspree.messageCaption` | |
| `formspree.messageRows` | |

### Footer (`params.footer`)

| Field | Notes |
|-------|--------|
| `recentPosts.enable` | |
| `recentPosts.path` | section path, example `"blogs"` |
| `recentPosts.count` | |
| `recentPosts.title` | |
| `recentPosts.disableFeaturedImage` | |
| `socialNetworks.github` / `linkedin` / `twitter` / `instagram` / `facebook` | URLs |

### List / single page prefs

| Param | Notes |
|-------|--------|
| `params.listPages.disableFeaturedImage` | |
| `params.singlePages.socialShare` | |
| `params.singlePages.readTime.enable` / `content` | |
| `params.singlePages.scrollprogress.enable` | |
| `params.singlePages.tags.openInNewTab` | |
| `params.terms.*` | `read`, `toc`, `copyright`, `emailText`, … |
| `params.datesFormat.article` / `articleList` / `articleRecent` | Go date layouts |

### Top-level Hugo config (non-params)

From `exampleSite/hugo.yaml`:

- `baseURL`, `languageCode`, `title`, `theme: hugo-profile`
- `languages` (en/es/fr) with `contentDir`, `menu.main`, and per-language `params` for translated sections
- `outputs.home`: HTML, RSS, JSON
- `pagination.pagerSize`
- `markup.goldmark.renderer.unsafe: true` (needed for some markdown-in-params)
- `Menus.main` (gallery etc.)
- Optional `services.googleAnalytics`, `services.disqus`

---

## Layouts, partials, assets

### Layouts (theme)

| Path | Role |
|------|------|
| `layouts/index.html` | Homepage sections |
| `layouts/404.html` | Not found |
| `layouts/_default/baseof.html` | Shell |
| `layouts/_default/list.html` | Section lists (blogs) |
| `layouts/_default/single.html` | Blog/page single |
| `layouts/_default/gallery.html` | Gallery |
| `layouts/_default/about.html` | About page chrome |
| `layouts/projects/list.html` | Projects section list |
| `layouts/shortcodes/dynamic-img.html` | Cloudinary image |
| `layouts/partials/head.html`, `head/extensions.html`, `scripts.html` | Head/scripts |
| `layouts/partials/sections/*` | Homepage + header/footer pieces |

### Static assets (`static/`)

Bootstrap 5, Font Awesome 6, theme CSS/JS, Viewer.js (`static/viewer/`), default favicon.

### i18n

`i18n/en.toml`, `es.toml`, `fr.toml` — nav labels, section titles, contact placeholders, common terms.

---

## Hugo Matters mapping notes

What the CMS / theme pack will eventually need:

1. **Site-config editing for nested `params` trees** — experience/education/projects/achievements are nested lists of objects, not flat front matter. The current pack only exposes a few hero fields; full Profile support means structured editors for the shapes above.
2. **Content types beyond post/page** — gallery (`layout` + `galleryImages`), optional projects type, about layout pages, and blog fields already partially modeled (`mathjax`, `toc`, tags, image).
3. **Path convention** — use `content/blogs` to match upstream (update pack `postsDirectory` when ready).
4. **Multilingual** — optional later; language-specific section copy lives under `languages.<code>.params`.
5. **Unsafe Goldmark** — Profile example enables it so markdown in params renders; document this for preview/build of Profile sites.

Related code today:

- Theme pack: `src/HugoMatters.ThemePacks/Packs/hugo-profile.json`
- Binder: `src/HugoMatters.ThemePacks/Packs/HugoProfileThemePack.cs`

---

## Demo data plan: Turpinverse → `hugo-matters-test`

### Planned test site (not created yet)

A future GitHub repo named **`hugo-matters-test`** will be a dummy Hugo site using the Hugo Profile theme. It will be populated from **Turpinverse-generated demo data** once Turpinverse can emit the missing Profile shapes.

Do **not** create `hugo-matters-test` until that data exists.

### What Turpinverse already has

Source: [markheydon/turpinverse](https://github.com/markheydon/turpinverse) (live docs: [turpinverse.uk](https://turpinverse.uk)).

| Area | Status | Notes |
|------|--------|--------|
| Personas (25) | Present | CRM + docs: name, title, bio, email, orgs, years, status |
| Organisations (10) | Present | Trading/legal names, industry, members, website |
| Timeline events (12) | Present | Dates, titles, persona links |
| Deals / cases | Present | CRM CSV export — not Profile sections |
| Hugo site | PaperMod docs site | `site/` generates personas/orgs/timeline markdown — **not** Hugo Profile |
| Experience / education / achievements / projects (portfolio) | **Missing** | Needed for Profile homepage params |
| Blog posts / gallery | **Missing** | Needed for Profile content demos |
| Hero / about / contact / skills / social params | **Missing as Profile shapes** | Bio/title/email can seed them but no generator output yet |

Turpinverse should grow **made-up but on-brand** Profile-oriented data (experiences, education, etc.) in isolation. Later, that data feeds `hugo-matters-test`.

### Gap → issue tracking

Work items for Turpinverse data generation live as GitHub issues on `markheydon/turpinverse` (search labels/titles for “Hugo Profile” / “hugo-matters”). This doc is the field-level contract those issues should follow.

---

## Source index (theme repo)

| Topic | Paths |
|-------|--------|
| Example config (authoritative shapes) | `exampleSite/hugo.yaml` |
| Example blogs / gallery | `exampleSite/content/` |
| Example images | `exampleSite/static/` |
| Homepage composition | `layouts/index.html` |
| Section partials | `layouts/partials/sections/` |
| Content layouts | `layouts/_default/`, `layouts/projects/` |
| Shortcode | `layouts/shortcodes/dynamic-img.html` |
| Archetype | `archetypes/default.md` |
| i18n | `i18n/*.toml` |
| Theme metadata | `theme.toml` |

Upstream README still documents Hugo ≥ 0.87; `theme.toml` lists `min_version = "0.68.0"`. Prefer the README requirement for new sites.
