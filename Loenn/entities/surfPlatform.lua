local utils = require("utils")

local surfPlatform = {}

surfPlatform.name = "rushHelper/surfPlatform"
surfPlatform.placements = {
	name = "platform",
	data = {
		width = 8,
		height = 8,
		color = "87CEFA",
		foreground = false
	}
}

surfPlatform.fieldInformation = {
	color = { fieldType = "color" }
}

surfPlatform.depth = function(room, entity)
	return entity.foreground and -19999 or -9999
end

local function withA(color, a)
	return { color[1] * a, color[2] * a, color[3] * a, a }
end

surfPlatform.fillColor = function(room, entity)
	return withA(utils.getColor(entity.color), 0.3)
end

surfPlatform.borderColor = function(room, entity)
	return withA(utils.getColor(entity.color), 0.8)
end

return surfPlatform