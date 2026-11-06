# Authored content

Files here are laid on top of the original assets when the runtime package is built
(`WC2.Tools build-pack`, run after every build). The folder mirrors the package layout:
`content/data/units.json` becomes `data/units.json` in the package. A file here wins over an original
asset placed at the same path. Markdown files are not packaged.
