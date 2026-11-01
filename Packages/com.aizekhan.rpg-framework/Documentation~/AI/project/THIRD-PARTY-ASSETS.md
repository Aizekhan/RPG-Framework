# Third-party assets

> Every model, prefab, texture, sound, music track, font, animation, shader or code taken from outside the team, or
> generated with a tool or AI service, has a row here **before** it is imported. Months later this table is the only proof that the game may use it: a store
> page changes, an author changes the licence, a release check asks where a file came from.

## Rules
1. **Read the licence before importing.** "Free download" is not a licence. Write down which licence the page gives
   and keep a copy of its text next to the files (`LICENSE.txt` in the asset's folder): the page can disappear.
2. **Ask the developer before importing** when the licence is missing or unclear, forbids commercial use (CC BY-NC,
   "personal use only", "editorial"), forbids changes (CC BY-ND) while the game changes the asset, or asks to share
   the game's own files under the same terms (CC BY-SA, GPL for code).
3. **Attribution** that a licence asks for (CC BY, many free sound and font licences) goes into the game's credits;
   write the exact text in the table.
4. **Store assets** (Unity Asset Store, Fab, ...) are licensed to the buyer, not to the public: never put them in a
   public repository. If the repository is public, keep them out of git and say here how to get them.
5. Keep imported assets in their own folder, `Assets/ThirdParty/<Source>/<Asset>/`, and name that folder in the
   asset's row. Architecture rule GA15 fails when an asset folder there has no row, or no copy of its licence
   (`LICENSE*`, `TERMS*`: text, PDF or screenshot) in its folder or once in the source's folder.
6. **A service's community** (Meshy Community, Sketchfab, ...): a model someone else published is an outside asset,
   even after you remesh, retexture or edit it, so its row goes in "In the game" under the community's licence, with
   the model's page and the author's name; "Changed by us" says what you did. Images in the same post can have other
   terms (non-commercial) than the model: do not use them unless their own licence allows it. Save the community's
   terms, or the service's written answer about them, once in the source's folder (`LICENSE-<what>.png/.pdf`).
   A free licence such as CC0 protects you only if the author had the right to publish: avoid models that copy a
   known character, brand or another game's asset.
7. **Generated assets** (Meshy, other AI generators, asset services) are not downloads, but the right to use them
   still comes from the service's terms, and those often depend on the plan at the moment of generation (a free plan
   may ask for attribution or make the asset public; a paid plan may give ownership). Write the tool, the plan, the
   date and the input; if the input was someone else's image or model, its rights apply too. Keep a copy of the terms
   that applied (a saved page or PDF). Stores may ask which content is AI-generated (Steam does): this table answers.
8. Assets the team made by hand need no licence; listing them as "own work" is optional.
9. Every attribution text in this file goes into `CREDITS.md` (the game's Credits screen).
10. An asset removed from the game moves to "Removed" with the date, so old builds can still be explained.

## In the game
| Asset | Folder in the project | Source page | Author | Licence (name, link) | Commercial use | Attribution text | Changed by us | Added |
|---|---|---|---|---|---|---|---|---|

## Generated with tools
| Asset | Folder in the project | Tool | Plan when generated | Date | Input (prompt, reference: whose) | Rights the terms give (link, saved copy) | Attribution text |
|---|---|---|---|---|---|---|---|

## Removed
| Asset | Why | Removed |
|---|---|---|
