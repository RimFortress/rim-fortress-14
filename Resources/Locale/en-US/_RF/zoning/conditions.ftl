zone-condition-base-progress = ({$current}/{$amount})

# Tile

zone-condition-tile-desc = 
    { $invert ->
        [true] Number of tiles at most {$amount}
       *[other] Number of tiles at least {$amount}
    }

# TileAdd

zone-condition-blocked-desc = 
    { $invert ->
        [true] Is not blocked
       *[other] Is blocked
    }

zone-condition-in-zone-desc =
    { $zones ->
        [empty] { $invert ->
            [true] { $amount ->
                [1] Not in any other zone
               *[other] Not in {$amount} any other zones
            }
           *[other] { $amount ->
                [1] In any other zone
               *[other] In {$amount} any other zones
            }
        }
       *[other] { $invert ->
            [true] { $amount ->
                [1] Not in zone of type: {$zones}
               *[other] Not in {$amount} zones of type: {$zones}
            }
           *[other] { $amount ->
                [1] In zone of type: {$zones}
               *[other] In {$amount} zones of type: {$zones}
            }
        }
    }

    Currently in: {$current ->
        [empty] none
        *[other] {$current}
    }

zone-condition-tile-entities-desc =
    { $types ->
        [empty] { $invert ->
            [true] { $amount ->
                [1] Tile empty
               *[other] Tile contains no more than {$amount} entities
            }
           *[other] { $amount ->
                [1] Tile not empty
               *[other] Tile contains at least {$amount} entities
            }
        }
       *[other] { $invert ->
            [true] { $amount ->
                [1] Tile doesn't contain entity of type: {$types}
               *[other] Tile contains no more than {$amount} entities of type: {$types}
            }
           *[other] { $amount ->
                [1] Tile contain entity of type: {$types}
               *[other] Tile contains at least {$amount} entities of type: {$types}
            }
        }
    }