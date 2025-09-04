pop-info-window-title = Character Info
pop-info-follow-button = Follow

pop-info-summary = {$name}, {$species}
    {$profession}
    Sex: {$sex ->
        [male] [color=#6495ED]Male[/color]
        [female] [color=#DDA0DD]Female[/color]
        *[other] [color=#808080]None[/color]
    }
    Age: {$age}

pop-info-skills-tab-title = Skills
pop-info-jobs-tab-title = Jobs
pop-info-health-tab-title = Health
pop-info-inventory-tab-title = Inventory

job-info-tab-current-task = [bold]Current task[/bold]
job-info-tab-task-none = [color=#808080][bold]None[/bold][/color]
job-info-tab-jobs-settings = Jobs settings

health-info-tab-status-label = [bold]Status:[/bold]
health-info-tab-temperature-label = [bold]Temperature:[/bold]
health-info-tab-blood-level-label = [bold]Blood Level:[/bold]
health-info-tab-damage-total-label = [bold]Total Damage:[/bold]

health-info-tab-status = {$status ->
    [alive] [color=#44944A]Alive[/color]
    [critical] [color=#C51D34][bold]Critical[/bold][/color]
    [dead] [color=#7442C8][bold]Dead[/bold][/color]
    *[other] [color=#808080]Unknown[/color]
}

health-info-tab-bleeding-alert = [color=#C51D34]{$name} is bleeding![/color]

health-info-tab-unknown-value = N/A
health-info-tab-damage-type = [bold]·[/bold] {$type}: {$amount}
