importScripts('daos.js', 'sha512.js');

let dao = null;

onmessage = function (e) {
    if (dao == null) {
        dao = new BasicNetworkDAO(
            e.data.authUrl,
            e.data.dbUrl
        );

        dao.setUsername(e.data.username);
        dao.hashed_password = e.data.hashedPassword;
    }

    dao.data(
        (data) => {
            //
            console.log(this, 'Just received data reply.');
            //
        },
        (res) => {
            //
            console.log(this, 'Data reply problem.', res);
            //
        }
    );
};