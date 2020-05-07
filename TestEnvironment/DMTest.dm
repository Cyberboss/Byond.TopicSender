/world/Topic(T)
	var/log_message = "Received: [T]"
	world.log << log_message

	var/list/as_params = params2list(T)
	if(as_params["return_float"])
		return 3.14

	return log_message
