return {
	name = "rushHelper/demon",
	placements = {
		name = "default",
		data = { dashRestores = 1 }
	},
	fieldInformation = {
		dashRestores = {
			fieldType = "integer",
			minimumValue = 0
		}
	},
	texture = "loenn/rushHelper/demon",
	color = function(room, entity)
		return entity.dashRestores == 0 and { 0, 1, 1 } or entity.dashRestores == 1 and { 1, 1, 1 } or entity.dashRestores == 2 and { 1, 0.4, 0.8 } or { 1, 0, 1 }
	end,
	depth = -100
}