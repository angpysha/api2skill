# Tasks: Multi-spec merge/generate bugfixes

## Phase 1: Bug 1 — tag path sanitization

- [x] T001 Add `ReferencePaths.SanitizeTagFileName` (+ collision map helper)
- [x] T002 Use sanitized names in `ReferenceWriter` and `SkillMdWriter` links
- [x] T003 Unit tests for `/` tags and link consistency

## Phase 2: Bugs 5–6 — auth profile attachment

- [x] T004 Extend `AttachmentResolver` with scheme-id scoping + unused-profile warnings
- [x] T005 Wire scheme ids from `SkillModelBuilder.ResolveExplicitAuth`
- [x] T006 Bake AuthProfileNames from scaffold / on-disk auth when no explicit auth config
- [x] T007 Tests: scheme-matched profiles, collision of true globals

## Phase 3: Bug 3 — composite baseUrl

- [x] T008 Treat non-HTTP(S) bases as unset; prefer HTTP(S); hard-error otherwise
- [x] T009 MergeCommand baseUrl selection from source manifests
- [x] T010 Composer tests for `file://` / mixed bases

## Phase 4: Bug 2 — spec cache for merge

- [x] T011 Write `.api2skill/specs/{id}.*` during generate (Composite + single)
- [x] T012 MergeCommand prefers cache paths
- [x] T013 Tests: cache write + merge offline preference

## Phase 5: Bug 4 — auth guidance

- [x] T014 Refresh `_guidance.schemes` from composed model into written auth.json
- [x] T015 Test prefixed scheme ids appear in guidance

## Phase 6: Ship

- [x] T016 Bump package version to 0.7.1
- [x] T017 Run unit tests (428 passed)
