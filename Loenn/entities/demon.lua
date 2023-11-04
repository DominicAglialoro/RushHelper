local demon = {}

demon.name = "rushHelper/demon"
demon.texture = "loenn/rushHelper/demon"
demon.depth = -100
demon.placements = {
	name = "demon",
	data = {
		dashRestores = 1
	}
}

demon.fieldInformation = {
	dashRestores = {
		fieldType = "integer",
		minimumValue = 0
	}
}

function demon.color(room, entity)
	return entity.dashRestores == 0 and { 0, 1, 1 } or entity.dashRestores == 1 and { 1, 1, 1 } or entity.dashRestores == 2 and { 1, 0.4, 0.8 } or { 1, 0, 1 }
end

return demon