local utils = require("utils")
local drawableSpriteStruct = require("structs.drawable_sprite")
local drawableRectangle = require("structs.drawable_rectangle")
local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableSprite = require("structs.drawable_sprite")

local pathDepth = 8999
local trailDepth = 8999
local blockDepth = -9999
local cardSwapBlock = {}

local function getSpritePathOrFallback(entity, customPath, fallback)
    local sprite = nil

    if customPath then
        customPath = string.gsub(customPath, "//", "/")

        sprite = drawableSpriteStruct.fromTexture(customPath, entity)
        if sprite then
            return customPath, sprite
        end

        customPath = customPath .. "00"
        sprite = drawableSpriteStruct.fromTexture(customPath, entity)
        if sprite then
            return customPath, sprite
        end
    end

    return fallback, drawableSpriteStruct.fromTexture(fallback, entity)
end

local function getCustomSpritePath(entity, spritePropertyName, spritePostfix, fallback)
    local sprite = nil

    local path = entity[spritePropertyName]
    if path then
        local fullPath = path .. (spritePostfix or "")

        return getSpritePathOrFallback(entity, fullPath, fallback)
    end

    return fallback, drawableSpriteStruct.fromTexture(fallback, entity)
end

local function getCustomSprite(entity, spritePropertyName, spritePostfix, fallback)
    local spritePath, sprite = getCustomSpritePath(entity, spritePropertyName, spritePostfix, fallback)

    if sprite and entity["tint"] then
        sprite:setColor(entity["tint"])
    end

    return sprite
end

cardSwapBlock.name = "rushHelper/cardSwapBlock"

cardSwapBlock.placements = {
	name = "default",
	data = {
		width = 16,
		height = 16,
		speed = 360,
		directory = "objects/swapblock",
		moveSFX = "event:/game/05_mirror_temple/swapblock_move",
		moveEndSFX = "event:/game/05_mirror_temple/swapblock_move_end",
		particleColor1 = "fbf236",
		particleColor2 = "6abe30",
		dashTrigger = true,
		emitParticles = true,
		renderBG = false
	}
}

cardSwapBlock.fieldOrder = {
	"x",
	"y",
	"width",
	"height",
	"speed",
	"directory",
	"moveSFX",
	"moveEndSFX",
	"particleColor1",
	"particleColor2",
	"dashTrigger",
	"emitParticles",
	"renderBG"
}

cardSwapBlock.fieldInformation = {
	particleColor1 = { fieldType = "color" },
	particleColor2 = { fieldType = "color" }
}

cardSwapBlock.nodeLineRenderType = "line"

cardSwapBlock.nodeLimits = {1, 1}

function cardSwapBlock.selection(room, entity)
    local main = utils.rectangle(entity.x, entity.y, entity.width, entity.height)

    if entity.nodes then
        local node = entity.nodes[1]
        return main, { utils.rectangle(node.x, node.y, entity.width, entity.height) }
    end

    return main, { }
end

local frameNinePatchOptions = {
    mode = "fill",
    borderMode = "repeat"
}

local trailNinePatchOptions = {
    mode = "fill",
    borderMode = "repeat",
    useRealSize = true
}

local pathNinePatchOptions = {
    mode = "fill",
    fillMode = "repeat",
    border = 0
}

local function addBlockSprites(sprites, entity, frameTexture)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 8, entity.height or 8

    local frameNinePatch = drawableNinePatch.fromTexture(frameTexture, frameNinePatchOptions, x, y, width, height)
    local frameSprites = frameNinePatch:getDrawableSprite()

    local lightsSprite = getCustomSprite(entity, "directory", "/midBlock00", "objects/swapblock/midBlock00")--drawableSprite.fromTexture(middleTexture, entity)

    lightsSprite:addPosition(math.floor(width / 2), math.floor(height / 2))
    lightsSprite.depth = blockDepth - 1

    for _, sprite in ipairs(frameSprites) do
        sprite.depth = blockDepth

        table.insert(sprites, sprite)
    end

    table.insert(sprites, lightsSprite)
end

local function addTrailSprites(sprites, entity, trailTexture, path)
    local nodes = entity.nodes or {}
    local x, y = entity.x or 0, entity.y or 0
    local nodeX, nodeY = nodes[1].x or x, nodes[1].y or y
    local width, height = entity.width or 8, entity.height or 8
    local drawWidth, drawHeight = math.abs(x - nodeX) + width, math.abs(y - nodeY) + height

    x, y = math.min(x, nodeX), math.min(y, nodeY)

    if path then
        local pathDirection = x == nodeX and "V" or "H"
        local pathTexture = getCustomSpritePath(entity, "directory", "/path" .. pathDirection, "objects/swapblock/path" .. pathDirection)--string.format("objects/swapblock/path%s", pathDirection)
        local pathNinePatch = drawableNinePatch.fromTexture(pathTexture, pathNinePatchOptions, x + 2, y + 2, drawWidth - 6, drawHeight - 6)
        local pathSprites = pathNinePatch:getDrawableSprite()

        for _, sprite in ipairs(pathSprites) do
            sprite.depth = pathDepth

            table.insert(sprites, sprite)
        end
    end

    local frameNinePatch = drawableNinePatch.fromTexture(trailTexture, trailNinePatchOptions, x - 1, y - 1, drawWidth, drawHeight)
    local frameSprites = frameNinePatch:getDrawableSprite()

    for _, sprite in ipairs(frameSprites) do
        sprite.depth = trailDepth

        table.insert(sprites, sprite)
    end
end

function cardSwapBlock.sprite(room, entity)
    local sprites = {}

    addTrailSprites(sprites, entity, getCustomSpritePath(entity, "directory", "/target", "objects/swapblock/target"), entity.renderBG)
    addBlockSprites(sprites, entity, getCustomSpritePath(entity, "directory", "/block", "objects/swapblock/block"))

    return sprites
end

function cardSwapBlock.nodeSprite(room, entity)
    local sprites = {}

    addBlockSprites(sprites, entity, getCustomSpritePath(entity, "directory", "/blockRed", "objects/swapblock/blockRed"))

    return sprites
end

return cardSwapBlock