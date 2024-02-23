local utils = require("utils")

local function withA(color, a)
	return { color[1] * a, color[2] * a, color[3] * a, a }
end

return {
	name = "rushHelper/surfPlatform",
	placements = {
		name = "default",
		data = {
			width = 8,
			height = 8,
			color = "87CEFA",
			foreground = false
		}
	},
	fieldInformation = {
		color = { fieldType = "color" }
	},
	fillColor = function(room, entity)
		return withA(utils.getColor(entity.color), 0.3)
	end,
	borderColor = function(room, entity)
		return withA(utils.getColor(entity.color), 0.8)
	end,
	depth = function(room, entity)
		return entity.foreground and -19999 or -9999
	end
}