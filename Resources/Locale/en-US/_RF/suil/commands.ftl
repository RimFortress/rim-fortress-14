cmd-rldsuil-desc = Reloads all SUIL styles.
cmd-rldsuil-help = Usage: {$command}

cmd-suil_reload-desc = Rebuilds the tree of SUIL views from their files without restarting. Without arguments waits for a click on a control.
cmd-suil_reload-help = Usage: {$command} [all | <path>]
    Without arguments: click a control to reload the SUIL view it belongs to (right click or Escape cancels).
    <path>: reload the views built from that file, like /Lobby/Screen.
    all: reload every view.
cmd-suil_reload-picking = Click a control to reload its view. Right click or Escape cancels.
cmd-suil_reload-none = There are no views of {$path} in the UI.
cmd-suil_reload-done = Reloaded {$path}.
cmd-suil_reload-failed = Could not reload {$path}, the tree is as it was:
cmd-suil_reload-total = Views reloaded: {$count}.
cmd-suil_reload-all-hint = every view
cmd-suil_reload-path-hint = path of a view
