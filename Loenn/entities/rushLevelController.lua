local rushLevelController = {}

rushLevelController.name = "rushHelper/rushLevelController"
rushLevelController.depth = -1000000
rushLevelController.texture = "loenn/rushHelper/rushLevelController"
rushLevelController.placements = {
	name = "controller",
	data = {
		requireKillAllDemons = true
	}
}

return rushLevelController