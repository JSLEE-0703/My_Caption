# Next Steps

Highest-value follow-up work:

1. Validate install, launch, offline translation, MDict lookup, and uninstall behavior with the generated Inno Setup installer.
2. Repeat installer validation on a clean Windows machine without system Python or development-only paths.
3. Decide whether the bundled `runtime` should stay mostly in normal Git, move more files to Git LFS, or move to a release artifact workflow.
4. Improve dictionary morphology fallback and support richer entry shapes.
5. Add better sentence segmentation for mixed English/Chinese streams.
6. Improve settings UX for language direction and provider-specific configuration.
7. Decide whether to keep the control panel or move to tray-first behavior.
