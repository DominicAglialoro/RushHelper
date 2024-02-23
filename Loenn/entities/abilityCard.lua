return {
	name = "rushHelper/abilityCard",
	placements = {
		{
			name = "yellow",
			data = { cardType = "Yellow" }
		},
		{
			name = "blue",
			data = { cardType = "Blue" }
		},
		{
			name = "green",
			data = { cardType = "Green" }
		},
		{
			name = "red",
			data = { cardType = "Red" }
		},
		{
			name = "white",
			data = { cardType = "White" }
		}
	},
	fieldInformation = {
		cardType = {
			options = {
				"Yellow",
				"Blue",
				"Green",
				"Red",
				"White"
			},
			editable = false
		}
	},
	texture = function(room, entity)
		return "loenn/rushHelper/card" .. entity.cardType
	end,
	depth = -101
}