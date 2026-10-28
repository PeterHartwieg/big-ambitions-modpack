# Screenshot index

All PNGs are native 1920 x 1080 Unity game-view captures. No resizing, annotation, relabeling or image editing. BEFORE is the supplied live 1.1.0 ZIP; AFTER is the supplied exact 1.1.1 ZIP. Capture timestamps and SHA-256 values are in screenshots-manifest.json.

Only completed checks appear below. UI actions used actual rendered Unity controls and callbacks because Windows foreground/capture failed. See README.md for fixture scope.

## M1

- [M1-before-restarted-apply.png](screenshots/M1-before-restarted-apply.png)
- [M1-before-restarted-check-again.png](screenshots/M1-before-restarted-check-again.png)
- [M1-after-restarted-apply.png](screenshots/M1-after-restarted-apply.png)
- [M1-after-restarted-check-again.png](screenshots/M1-after-restarted-check-again.png)

## M2

- [M2-before-cold-apply.png](screenshots/M2-before-cold-apply.png)
- [M2-before-post-city-apply.png](screenshots/M2-before-post-city-apply.png)
- [M2-after-cold-apply.png](screenshots/M2-after-cold-apply.png)
- [M2-after-post-city-apply.png](screenshots/M2-after-post-city-apply.png)

## M3

- [M3-after-persisted-merged-settings.png](screenshots/M3-after-persisted-merged-settings.png)
- [M3-after-check-again-status.png](screenshots/M3-after-check-again-status.png)

## M4

- [M4-after-debt-start-city.png](screenshots/M4-after-debt-start-city.png)
- [M4-after-scenario-option-off.png](screenshots/M4-after-scenario-option-off.png)
- [M4-after-debt-off-control.png](screenshots/M4-after-debt-off-control.png)
- [M4-after-campus-diplomas.png](screenshots/M4-after-campus-diplomas.png)
- [M4-after-checkout-refused-no-diploma.png](screenshots/M4-after-checkout-refused-no-diploma.png)
- [M4-after-qualified-checkout-accepted.png](screenshots/M4-after-qualified-checkout-accepted.png)
- [M4-after-native-pickup-refused.png](screenshots/M4-after-native-pickup-refused.png)
- [M4-after-native-qualified-pickup.png](screenshots/M4-after-native-qualified-pickup.png)
- [M4-after-kiln-pickup-refused.png](screenshots/M4-after-kiln-pickup-refused.png)
- [M4-after-kiln-pickup-allowed.png](screenshots/M4-after-kiln-pickup-allowed.png)
- [M4-after-polisher-pickup-refused.png](screenshots/M4-after-polisher-pickup-refused.png)
- [M4-after-polisher-pickup-allowed.png](screenshots/M4-after-polisher-pickup-allowed.png)

## M5

- [M5-after-normal-save-complete.png](screenshots/M5-after-normal-save-complete.png)
- [M5-after-reloaded-city.png](screenshots/M5-after-reloaded-city.png)

## M6

- [M6-after-default-on.png](screenshots/M6-after-default-on.png)
- [M6-after-option-on-night.png](screenshots/M6-after-option-on-night.png)
- [M6-after-option-off-night.png](screenshots/M6-after-option-off-night.png)
- [M6-after-night-unmuted.png](screenshots/M6-after-night-unmuted.png)

## M7

- [M7-before-takeover-rule-on.png](screenshots/M7-before-takeover-rule-on.png)
- [M7-before-rule-on-gate-lost.png](screenshots/M7-before-rule-on-gate-lost.png)
- [M7-after-two-copies-one-page.png](screenshots/M7-after-two-copies-one-page.png)
- [M7-after-takeover-rule-on.png](screenshots/M7-after-takeover-rule-on.png)
- [M7-after-takeover-gate-refuses.png](screenshots/M7-after-takeover-gate-refuses.png)
- [M7-after-takeover-gate-allows.png](screenshots/M7-after-takeover-gate-allows.png)
- [M7-after-legacy-stand-down.png](screenshots/M7-after-legacy-stand-down.png)

## Reading the evidence

The destination PNG was SHA-256 compared to the native source at capture time and is never overwritten. Some helper source-path PNGs were later overwritten when a pending screenshot request replayed at a subsequent launch. The manifest records both hashes and the capture receipts; the published destination bytes still match their original capture-time checksums. No image was relabeled.

M4 checkout uses a native PurchaseUI with one sewing machine and a recording order callback. Its $100 price is a test fixture, not the depot price. Actual depot pickup screenshots show the native $95,000 sewing machine and other native machine prices. Passed diploma results were seeded into RLO; attendance and exams were not simulated.

M6 uses source state as permitted proof, not recorded sound. The rig global output remained muted. The exact cricket source plays at volume 0.09 with its mute bit on/off/on.

M7 uses an exact second package with a Workshop-shaped discovery descriptor and the native lifecycle loader, not Steam discovery. The legacy case has one legacy gameplay page plus the pack member-checker page; the pack has zero gameplay toggles and zero gameplay hooks.
