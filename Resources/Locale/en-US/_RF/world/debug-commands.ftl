cmd-showsettlements-desc = Shows the settlements: their color, the area they reach, what they own and what it is worth
cmd-showsettlements-help = Usage: {$command}
    Turns the overlay of the settlements on and off.

cmd-showengagements-desc = Shows the situations (engagements): who is in them, in which roles, who owns them and what the participants do
cmd-showengagements-help = Usage: {$command} [kinds|off]
    Without an argument turns the overlay on and off. The kinds are the parts of the names of the situations, separated by commas, such as Raid,Squad; "off" turns the overlay off.

cmd-pathdebug-desc = Debug overlays of the pathfinding: the navigation map, what the nodes cost, the influence of a tag, the routes of the agents
cmd-pathdebug-help = Usage: {$command} <mesh|cost|influence|routes> [on|off]
    {$command} profile|query|tag|agent <id|->     chooses what the overlays are for ("-" for the default)
    {$command} explored [on|off]                  the nodes that the last search of a route looked at
    {$command} status | off
    mesh: the polys of the navigation map and what is in them. cost: what the nodes cost an agent by a profile and a query (the agent is the
    one chosen, otherwise the closest to the middle of the screen). influence: a baked tag. routes: the paths that the agents follow, and
    the nodes that their searches looked at (for the chosen agent, query and profile, otherwise all).
cmd-pathdebug-unknown = There is no {$kind} {$id}
cmd-pathdebug-status = Layers: {$layers}; profile: {$profile}; query: {$query}; tag: {$tag}; agent: {$agent}; explored nodes: {$explored}
