local fakeTilesHelper = require("helpers.fake_tiles")

return {
	name = "rushHelper/rushDashBlock",
	placements = {
		name = "default",
		data = {
			tiletype = "9",
			blendin = false,
			canDash = false,
			permanent = false,
			width = 8,
			height = 8
		}
	},
	fieldInformation = fakeTilesHelper.getFieldInformation("tiletype"),
	sprite = fakeTilesHelper.getEntitySpriteFunction("tiletype", "blendin"),
	depth = 0
}