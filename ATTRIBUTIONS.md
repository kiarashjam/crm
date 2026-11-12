# Third-party attributions

The **Cadence application** (frontend and backend) is built from scratch and is not derived from or copied from any other CRM or product. The following are the only third-party assets or components used:

- **shadcn/ui** — UI components (e.g. dialogs, dropdowns, forms) from [shadcn/ui](https://ui.shadcn.com/) used under [MIT license](https://github.com/shadcn-ui/ui/blob/main/LICENSE.md).
- **UI/UX Pro Max** — the Claude Code design skill in `.claude/skills/ui-ux-pro-max` (version 2.15.0, commit 28b3b8d), from [nextlevelbuilder/ui-ux-pro-max-skill](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill), used under the MIT license included beside it. It is a development aid only; nothing from it ships in the app.
- **Unsplash** — Photos used in the Figma design file from [Unsplash](https://unsplash.com) under [Unsplash license](https://unsplash.com/license).
- **py_webauthn test recordings** — three WebAuthn answers that real browsers gave (a security key's registration and two sign-ins), taken from the test suite of [py_webauthn](https://github.com/duo-labs/py_webauthn) 2.5.2 into `backend/tests/ACI.WebApi.Tests/Fixtures/Passkeys/browser-recordings.json`, where the passkey tests replay them. Copyright (c) 2017-2021 Duo Security, Inc., used under the BSD 3-Clause license. Test data only; nothing from it ships in the app.