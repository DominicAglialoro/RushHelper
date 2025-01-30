local drawableSpriteStruct = require('structs.drawable_sprite')

return {
	name = "rushHelper/rushStartLine",
	placements = {
		name = "default",
		data = { height = 8 }
	},
	sprite = function(room, entity)
		local x, y = entity.x or 0, entity.y or 0
        local height = entity.height or 8
    
        local startX, startY = math.floor(x / 8) + 1, math.floor(y / 8) + 1
        local stopY = startY + math.floor(height / 8) - 1
		local len = stopY - startY
		
		local sprites = {}
    
        for i = 0, len do
            local sprite = drawableSpriteStruct.fromTexture("loenn/rushHelper/rushStartLine", entity)
    
            sprite:setJustification(0, 0)
            sprite:addPosition(0, i * 8)
			sprite:useRelativeQuad(0, 0, 8, 8)
    
            table.insert(sprites, sprite)
        end
    
        return sprites
	end,
	depth = -100,
	selection = function(room, entity)
        return utils.rectangle(entity.x, entity.y, 8, entity.height)
    end
}