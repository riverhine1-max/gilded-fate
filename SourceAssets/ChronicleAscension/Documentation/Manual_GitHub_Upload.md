# Manual upload

1. Download and extract both ZIPs. Each begins with `SourceAssets/ChronicleAscension/`; merge their folders without adding another enclosing ZIP-name folder.
2. Copy that new folder into a current local checkout of `riverhine1-max/gilded-fate`. Inspect the changes: only this staging folder should be added. The extracted content belongs outside live `Assets/Resources` until Claude integrates it.
3. Upload the extracted files with your normal Git workflow, or use GitHub's file upload interface where it supports your folder selection. Give Claude the repository path and `Documentation/Claude_Prompt4_Integration.md`.

For a PC checkout, the scoped Git commands are:

```sh
git status --short
git add SourceAssets/ChronicleAscension
git commit -m "Add Chronicle Ascension staging content and new narration"
git push
```

These are instructions for you; no GitHub write, commit, push, pull request, or Unity import was performed by this production task. Uploading only the ZIP files stores an archive, rather than exposing the extracted content files to the eventual loader.
