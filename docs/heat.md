# How heat works

Every treemap block is coloured by how much bitrate a file spends per pixel per frame — a proxy for "is this file bloated for what it shows," independent of resolution or runtime.

## The formula

```
bpp = video bitrate ÷ (width × height × frame rate)
normalised bpp = bpp ÷ codec factor
```

`bpp` alone isn't comparable across codecs, since codecs differ in how efficiently they turn bits into picture. Spacearr divides by a factor so codecs land on one scale:

| Codec | Factor |
|---|---|
| H.264 | 1.00 |
| HEVC | 0.60 |
| AV1 | 0.50 |
| VP9 | 0.65 |
| MPEG-2 | 1.50 |
| VC-1 | 1.20 |

A factor below 1 means the codec is more efficient than H.264, so it "should" need fewer bits for the same quality — dividing raises its normalised value, so an HEVC file spending as many bits as an H.264 file reads hotter, since that's relatively more wasteful for HEVC. A factor above 1 (older codecs) does the opposite.

## Relative vs absolute

**Relative mode** (the default) ranks every readable file in view by percentile and spreads them green-to-red across that ranking — always the full colour range, best for finding your worst outliers regardless of what "worst" means in absolute terms.

**Absolute mode** maps normalised bpp to fixed stops: 0.04 and below is green, 0.10 a third of the way to red, 0.20 two-thirds, 0.30 and above fully red. It's comparable across scans and libraries, but a mostly-remux library (bitrate close to the source disc) looks mostly, uniformly red — remuxes are *supposed* to spend a lot of bpp, so that's correct. Switch to relative mode there to see which remuxes are relatively worse; absolute mode has no room left to discriminate between them.

## "Unreadable"

A file ffprobe couldn't read — corrupt, unsupported container, a permissions error — has no bitrate or resolution to compute from. It's shown grey (`#6B7280`), apart from the colour scale, never guessed at. The detail panel shows why, when known.

## Limits

- **Variable frame rate** sources report an average that may not match playback timing, over- or under-stating bpp.
- **Interlaced content** halves the visible resolution per field; uncorrected, it can read hotter than it perceptually is.
- **Animation and low-motion content** genuinely needs fewer bits, so it sits at a low bpp even when well encoded — green there isn't automatically "fine"; the scale isn't content-aware.
