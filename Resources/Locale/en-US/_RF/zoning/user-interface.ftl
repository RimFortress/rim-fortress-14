zone-creation-widget-finish = Create a zone
zone-creation-widget-cancel = Cancel
zone-conditions-label = [bold]Conditions:[/bold]
zone-tile-conditions-popup-body = Tile is blocked, not all conditions have been met:

base-zone-window-title = Zone
base-zone-window-name-label = [bold]Name:[/bold]
base-zone-window-content-title = [bold]Content:[/bold]
base-zone-window-visuals-tab-general-title = General
base-zone-window-visuals-tab-visuals-title = Visuals
base-zone-window-visuals-save-button = Save
base-zone-window-visuals-reset-button = Reset
base-zone-window-expand-button-tooltip = Expand Zone
base-zone-window-shrink-button-tooltip = Shrink Zone
base-zone-window-delete-button-tooltip = Delete Zone

zone-content-name-label =
    { $amount ->
        [1] [bold]{$name}[/bold]
       *[other] [bold]{$name}[/bold] (x{$amount})
    }

zone-visuals-border-color-button = Border color
zone-visuals-zone-color-button = Inner color
zone-visuals-show-tile-borders-button = Show tile borders
zone-visuals-show-conditions-button = Show conditions
zone-visuals-layer-enable-button = Enable
zone-visuals-state =
    { $state ->
        [base] [bold]Default[/bold]
        [picking] [bold]Zone Picking[/bold]
        [selected] [bold]Selected[/bold]
        [invalid] [bold]Invalid[/bold]
       *[other] ???
    }