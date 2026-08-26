window.loginUser = async function (username, password) {

    console.log("JS USER:", username);
    console.log("JS PASSWORD:", password);

    const response = await fetch("/api/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            Login: username,
            Password: password
        })
    });

    console.log("HTTP STATUS:", response.status);

    return response.ok;
};