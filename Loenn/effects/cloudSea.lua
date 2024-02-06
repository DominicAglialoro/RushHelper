local cloudSea = { }

cloudSea.name = "rushHelper/cloudSea"
cloudSea.canBackground = true
cloudSea.canForeground = false

cloudSea.fieldOrder = {
	"nearY",
	"farY",
	"nearScrollY",
	"farScrollY",
	"layerCount",
	"layerHeight",
	"layerNearColor",
	"layerFarColor",
	"layerBottomValue",
	"waveCount",
	"waveNearScroll",
	"waveFarScroll",
	"waveNearScale",
	"waveFarScale",
	"waveMinAmplitude",
	"waveMaxAmplitude",
	"waveMinFrequency",
	"waveMaxFrequency",
	"waveMinSpeed",
	"waveMaxSpeed",
	"flip"
}

cloudSea.fieldInformation = {
	nearY = { fieldType = "integer" },
	farY = { fieldType = "integer" },
	layerCount = { fieldType = "integer" },
	layerHeight = { fieldType = "integer" },
	layerNearColor = { fieldType = "color" },
	layerFarColor = { fieldType = "color" },
	waveCount = { fieldType = "integer" }
}

cloudSea.defaultData = {
	nearY = 0,
	farY = 0,
	nearScrollY = 0,
	farScrollY = 0,
	layerCount = 1,
	layerHeight = 16,
	layerNearColor = "ffffff",
	layerFarColor = "ffffff",
	layerBottomValue = 1,
	waveCount = 1,
	waveNearScroll = 0,
	waveFarScroll = 0,
	waveNearScale = 1,
	waveFarScale = 1,
	waveMinAmplitude = 0,
	waveMaxAmplitude = 0,
	waveMinFrequency = 0,
	waveMaxFrequency = 0,
	waveMinSpeed = 0,
	waveMaxSpeed = 0,
	flip = false
}

return cloudSea