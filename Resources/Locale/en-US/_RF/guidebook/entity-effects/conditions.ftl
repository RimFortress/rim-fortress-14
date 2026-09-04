entity-condition-guidebook-total-need =
    { $max ->
        [2147483648] the target has at least {NATURALFIXED($min, 2)} total {$type}
        *[other] { $min ->
                    [0] the target has at most {NATURALFIXED($max, 2)} total {$type}
                    *[other] the target has between {NATURALFIXED($min, 2)} and {NATURALFIXED($max, 2)} total {$type}
                 }
    }
