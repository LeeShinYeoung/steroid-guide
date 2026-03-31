# Build Report

## Implemented
- [x] Fixed pagination arrow centering by deriving the glyph width from the widest shared arrow row before mirroring left/right geometry.

## Files Changed
- `Common/UI/UIPaginationArrowButton.cs` — corrected arrow centering math so both mirrored glyphs use the same true width inside the existing button bounds.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide UI in-game and navigate to a paginated craftable item list.
3. Verify the previous/next arrows are centered, visually symmetric, and still respond correctly to hover, disabled, click, and scroll-wheel pagination.

## Known Issues
- None.
