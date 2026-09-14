zone-condition-base-progress = ({$current}/{$amount})

zone-condition-tile-tile-add-desc = 
    { $invert ->
        [true] The tile type is not one of the following: {$types}
       *[other] The type of tile is one of the following: {$types}
    }

zone-condition-tile-tile-desc = 
    { $types ->
        [empty] { $invert ->
            [true] Number of tiles at most {$amount}
           *[other] Number of tiles at least {$amount}
        }
       *[other] { $invert ->
            [true] { $amount ->
                [1] Doesn't contain tile of type: {$types}
               *[other] Contains no more than {$amount} tile of type: {$types}
            }
           *[other] { $amount ->
                [1] Contain tile of type: {$types}
               *[other] Contains at least {$amount} tile of type: {$types}
            }
        }
    }

zone-condition-blocked-tile-desc = 
    { $invert ->
        [true] { $amount ->
            [1] Any tile is not blocked
           *[other] At least {$amount} tiles are not blocked
        }
       *[other] { $amount ->
            [1] Any tile is blocked
           *[other] At least {$amount} tiles are blocked
        }
    }

zone-condition-blocked-tile-add-desc = 
    { $invert ->
        [true] Is not blocked
       *[other] Is blocked
    }

zone-condition-in-zone-tile-add-desc =
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

zone-condition-neighbor-tile-add-desc =
    { $invert ->
        [true] { $amount ->
            [1] Any neighboring tile doesn't satisfying the following conditions:
           *[other] {$amount} neighboring tiles doesn't satisfying the following conditions:
        }
       *[other] { $amount ->
            [1] Any neighboring tile satisfying the following conditions:
           *[other] {$amount} neighboring tiles satisfying the following conditions:
        }
    }

zone-condition-or-desc =
    { $invert ->
        [true] The following conditions are not met:
       *[other] Any condition from the list is met:
    }

zone-condition-prototyped-tile-add-desc =
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

zone-condition-prototyped-entity-desc =
    { $invert ->
        [true] { $amount ->
            [1] Zone doesn't contain entity of type: {$types}
           *[other] Zone contains no more than {$amount} entities of type: {$types}
        }
       *[other] { $amount ->
            [1] Zone contain entity of type: {$types}
           *[other] Zone contains at least {$amount} entities of type: {$types}
        }
    }
