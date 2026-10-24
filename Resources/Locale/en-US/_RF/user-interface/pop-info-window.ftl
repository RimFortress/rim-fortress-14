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
pop-info-mood-tab-title = Mood
pop-info-opinion-tab-title = Opinion
pop-info-inventory-tab-title = Inventory

job-info-tab-current-task = [b]Current task[/b]
job-info-tab-task-none = [color=#808080][b]None[/b][/color]
job-info-tab-jobs-settings = Jobs settings

health-info-tab-status-label = [b]Status:[/b]
health-info-tab-temperature-label = [b]Temperature:[/b]
health-info-tab-blood-level-label = [b]Blood Level:[/b]
health-info-tab-damage-total-label = [b]Total Damage:[/b]

health-info-tab-status = {$status ->
    [alive] [color=#44944A]Alive[/color]
    [critical] [color=#C51D34][b]Critical[/b][/color]
    [dead] [color=#7442C8][b]Dead[/b][/color]
    *[other] [color=#808080]Unknown[/color]
}

health-info-tab-bleeding-alert = [color=#C51D34]{$name} is bleeding![/color]

health-info-tab-unknown-value = N/A
health-info-tab-damage-type = [b]·[/b] {$type}: {$amount}

mood-info-total = [b]Total: [color={$color}]{$mood}[/color][/b]

effect-never-end = –
