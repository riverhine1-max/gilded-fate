# Unity license for the cloud builds

Status: **guidance only; nothing here changes how builds log in today.**

## How the builds log in now

`ci/unity-build.sh` looks for credentials in this order:

1. **`UNITY_LICENSE`** (a Unity license file, `.ulf`, pasted as a repository secret). If present, it is used and nothing is activated or returned.
2. Otherwise **`UNITY_EMAIL` + `UNITY_PASSWORD`**, which activate a Personal seat for the build and return it afterwards. This is what you use today. It requires two-step verification to be off on that Unity account.

## Safer option you can take today (no new license file)

Create a **separate Unity account used only for CI** (not your main account), keep two-step verification off on that one only, and put its email and password in the secrets instead. If those secrets ever leaked, the damage is limited to a throwaway account with no purchases or other projects. Rotate the password occasionally.

Worth knowing: `build-game.yml` runs only on pushes to `main` and manual runs. It does not run on pull requests, so code from a pull request or a fork never gets to read the secrets. The new `chronicle-tests.yml` uses no secrets at all.

## Using a license file instead

I am **not certain** how to obtain a `.ulf` for Unity 6000.5 Personal in 2026. Unity has changed its licensing tools several times, and the old manual-activation page may no longer work. If you find a current Unity or game-ci procedure that gives you a valid `.ulf`:

1. Add its full contents as a repository secret named `UNITY_LICENSE` (Settings > Secrets and variables > Actions).
2. Run the workflow with target `windows-qa` (about 19 minutes) and confirm it goes green.
3. If it fails with a license error, delete the `UNITY_LICENSE` secret. The build falls straight back to email and password, so nothing is lost.

Do not remove the email and password secrets until a build has succeeded with the license file.
