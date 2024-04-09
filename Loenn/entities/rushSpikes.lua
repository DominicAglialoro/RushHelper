local spikeHelper = require("helpers.spikes")

local spikeOptions = {
	directionNames = {
		up = "rushHelper/rushSpikesUp",
		down = "rushHelper/rushSpikesDown",
		left = "rushHelper/rushSpikesLeft",
		right = "rushHelper/rushSpikesRight"
	},
	placementName = "default",
	variants = { "default" }
}

return spikeHelper.createEntityHandlers(spikeOptions)