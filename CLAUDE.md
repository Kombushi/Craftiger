# GTNH Crafting Planner

Crafting cost planner for GregTech: New Horizons 2.9.0-beta2: prices every
craftable item from leaf materials under the user's machine garage and renders
BOMs as flat icon grids.

**`spec.md` is the source of truth.** Read the relevant section before
implementing anything. When code and spec disagree, the spec wins; when
behavior must change, update `spec.md` in the same commit as the code.

## Layout

- `src/Craftiger.Builder/` — standalone .NET console. NESQL dump in →
  `planner.sqlite` + `atlas.webp` + `atlas-offsets.json` out. Runs offline,
  is never deployed, and has no project references to or from the API. The
  three artifacts are the only contract (spec §3).
- `src/Craftiger.Solver/` — pure class library: cost engine and BOM walk. No
  I/O, no dump dependency; referenced by the API, tested against fixtures
  (spec §8).
- `src/Craftiger.Api/` — .NET minimal API. Stateless; loads the artifacts
  read-only; hosts the solver and its solve cache in memory (spec §8).
- `web/` — React SPA. All user state lives in browser `localStorage` and
  travels with each request; the API stores nothing per user.
- `tests/Craftiger.Builder.UnitTests/` — xUnit tests for the Builder; Solver
  and API tests get sibling projects under `tests/`.

## Invariants — do not "improve" these

- The solver updates only on strict improvement:
  `candidate < cost[item] − 1e-9`. Ties lose. Acyclicity of `bestRecipe`
  depends on this (spec §5).
- BOM totals are computed on the `bestRecipe` DAG in reverse topological
  order — never by walking a UI structure (spec §6).
- Chanced outputs divide by `chance` (expected value) in both prices and run
  counts (spec §4, §6).
- No byproduct credit: each output is priced from the recipe's full input cost.
- Pins overlay recipe choice only: never part of the solve cache key, never
  able to bypass garage legality (spec §5).
- Every "does not / never" product rule lives in spec §9. Add new ones there,
  nowhere else.
- All modpack parsing happens offline in the builder; runtime consumes only
  the three artifacts.

## Glossary

Tier ladder (`Steam = 0, LV = 1, …`), garage, garage-legal, upstream closure,
leaf, BOM, pin, `solveId` — definitions in spec §2. Use these exact names for
types and identifiers.

## Stack

- .NET 10, `Microsoft.Data.Sqlite` with Dapper for all database access,
  `Microsoft.Extensions.DependencyInjection`, xUnit.
- Task runner: `mise`. Every build/test/run command is a named `mise` task;
  docs reference targets by name only. Targets to create with the scaffold:
  `build`, `test`, `builder`, `api`, `web`.
- The NESQL dump is an HSQLDB database (Java-only format, ~600 MB), converted
  once into a local `dump.sqlite` by a throwaway JDBC copy (HSQLDB driver,
  `jdbc:hsqldb:file:…`, user `sa`, empty password) — the only step that ever
  needs a JRE, not part of the repo's task set. All code and ad-hoc queries
  read the SQLite copy: use `sqlite3` with `LIMIT`, never dump whole tables.

## Testing

- The real NESQL dump is huge and never committed. Builder and solver tests
  run on a small hand-written fixture dump covering: an ingot↔block cycle, a
  chanced output, an EBF heat recipe, a `None` machine, and a pinned recipe.
- Every acceptance check in spec §10 gets at least one automated test.

## Conventions

- C# style: Allman braces with 4-space indent; every `if`/`for`/`foreach`
  body is a braced block, even single statements. Every file ends with a
  newline. Encoded in `.editorconfig`.
- Executables compose via `IHost`, take their settings from `appsettings.json`
  through `IOptions`, and are laid out as `Models/`, `Interfaces/`,
  `Services/`, `Repositories/` — one type per file, every service and
  repository behind an interface. Logging goes through `ILogger`, never
  `Console`.
- Comments explain why, not what. No decorative comments, no narration of
  changes, no session references; docs and comments are timeless.
- Commit messages: header in past tense, sentence case, ≤ 50 chars after the
  prefix; prefix is a real task ID from context if one exists, otherwise a
  Conventional Commits type (`feat:`, `fix:`, …). Body only for big commits,
  as a bullet list wrapped at 72. Never add Claude attribution footers,
  session URLs, or `Co-Authored-By` trailers.
- Code, identifiers, and UI strings are in English.

## Implementation order

1. Builder (real `planner.sqlite` early — dump schema surprises surface first)
2. Solver core against fixtures (pure, fully testable without a dump)
3. API
4. Web
