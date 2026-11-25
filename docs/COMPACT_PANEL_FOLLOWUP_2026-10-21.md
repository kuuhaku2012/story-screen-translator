# Compact-panel user follow-up

New diagnostic: `artifacts/ocr-diagnostics/20261021-154457-72782ef9fc6d4128a0440eb66fc4c2e5`. Source PNG inspected. Entire intended page is inside selected661x921 region; no evidence that region selection clipped the missing dialogue. Windows OCR comic mode107.52ms,16 groups. JSON confirms new compact layout is running: narrow MY19x10→27x15.5, DON'T40x52→48x60; previous90px minimum gone.

The user's complaint that boxes do not cover all text is valid. Source OCR never includes several visible words: DON'T PLAY DUMB WITH ME loses ME and misreads DUMB; WANNA GO FOR A DRINK becomes FOR A PRINK; I'M SERIOUS becomes sszoug. Other balloons are fragmented. Those missing source words have no source bounds to cover. Compact padding cannot recover absent detection or unify whole speech balloons. Expanding all cards would obscure artwork without validating text coverage.

This export contains source OCR and geometry predictions, not translated text/font-fit output, so it cannot establish whether a specific Vietnamese sentence overflows in live WPF. A complete balloon/text-region detector distinct from Windows OCR word detection is still absent. Current compact layout is a successful geometry-size change only, not acceptance of manga recognition or full-dialogue coverage.

Next technical dependency: obtain complete local region proposals independently, recognize full content in each, and validate recall/split/merge on this actual capture plus unseen layouts. Any speech-bubble segmentation candidate must also handle connected bubbles and text outside bubbles and meet runtime limits. Prior slow CTD and incomplete Rapid configurations are not accepted substitutes. No app/package change or API call in this follow-up review.
