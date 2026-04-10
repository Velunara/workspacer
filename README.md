<div align="center">
  <a href="https://workspacer.org" target="_blank">
    <img alt="Stable action status" src="https://raw.githubusercontent.com/workspacer/workspacer/master/images/logo-wide.svg">
  </a>
  <p>
    <i>a tiling window manager for Windows 10+</i>
  <p>
</div>

---

__workspacer__ is a tiling window manager for Windows 10+, this fork replicats the functionality of window managers like AwesomeWM and Hyprland+Hyprsplit. Improves mouse interactability, adds support for AltDrag and improves workspacers ability to remember where windows were before it was killed. And well probably more that I've just forgotten.

# Installation

This fork currently has to be built from source.

# Customization

Adapt workspacer to your workflow using its rich scriptable API.

Workspacer provides sensible defaults with low code:

```cs
Action<IConfigContext> doConfig = (context) =>
{
    // Uncomment to switch update branch (or to disable updates)
    //context.Branch = Branch.None;

    context.AddBar();
    context.AddFocusIndicator();
    var actionMenu = context.AddActionMenu();

    context.WorkspaceContainer.CreateWorkspaces("1", "2", "3", "4", "5");
    context.CanMinimizeWindows = true; // false by default
    context.UseAltDrag(KeyModifiers.Alt); // Enable alt drag to move and resize windows.
};
return doConfig;
```

This gives you a full experience, but you can read the [config][config-page]
page to see the full gambit of available options.

Check out the [wiki][wiki-page] to see other users'
configurations and post your own!

# Contributing

Thanks for your interest in contributing!

Review the [code of conduct](./CODE_OF_CONDUCT.md) and submit a pull request!

# Community

You may join in our unofficial community chat hosted on the [matrix-platform](https://matrix.org/).

Our community can be found on [#workspacer-community:matrix.org](https://matrix.to/#/#workspacer-community:matrix.org).

[workspacer-home]: https://workspacer.org
[quickstart-page]: https://workspacer.org/quickstart
[config-page]: https://workspacer.org/config
[wiki-page]: https://github.com/workspacer/workspacer/wiki/Customization
