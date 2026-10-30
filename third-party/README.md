# Third-party code

## ServerSync

`src/MarkOfOden/ServerSync.cs` is [ServerSync](https://github.com/blaxxun-boop/ServerSync) by blaxxun,
vendored as a single source file rather than taken as a dependency, which is how that project is meant
to be used. It handles server-authoritative config and the version check between client and server.

Its licence is in `ServerSync-LICENSE.txt` and applies to that file only.

## LocalizationManager

`src/MarkOfOden/Localization/LocalizationManager.cs` is
[LocalizationManager](https://github.com/blaxxun-boop/LocalizationManager) by blaxxun, vendored as
source and lightly adapted (the file's header lists the changes). It loads the translations in
`translations/`. Its licence is in `LocalizationManager-LICENSE.txt` and applies to that file only.

## YamlDotNet

[YamlDotNet](https://github.com/aaubry/YamlDotNet) reads the translation files. It comes from NuGet
and is merged into `MarkOfOden.dll` at build time with ILRepack. Its licence is in
`YamlDotNet-LICENSE.txt`.

Everything else in this repository is under the MIT licence in `LICENSE`. All three licences above
are also shipped with the mod, in `THIRD-PARTY-NOTICES.md`.
