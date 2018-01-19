class DatabaseNetworkDAO {
    constructor(
        base_dao
    ) {
        this.dao = base_dao;
    }

    data(success, failure) {
        this.dao.authenticatedTransaction(
            '/data',
            {
            },
            (resp) => {
                success(JSON.parse(resp.test));
            },
            (res) => {
                failure(res);
            }
        );
    }
}
  
class AuthNetworkDAO {
    constructor(
        url_auth
    ) {
        this.dao = new BasicNetworkDAO(
            url_auth,
            url_auth
        );
    }

    getDatabaseDAO(url) {
        return new DatabaseNetworkDAO(this.dao.clone(url));
    }

    userSet(user, success, failure) {
        this.dao.authenticatedTransaction(
            '/user-set',
            {
                user: user,
            },
            (resp) => {
                success();
            },
            (res) => {
                failure(res);
            }
        );
    }

    userDelete(username, success, failure) {
        this.dao.authenticatedTransaction(
            '/user-delete',
            {
                username: username,
            },
            (resp) => {
                success();
            },
            (res) => {
                failure(res);
            }
        );
    }

    userList(success, failure) {
        this.dao.authenticatedTransaction(
            '/user-list',
            {},
            (resp) => {
                success(JSON.parse(resp.text));
            },
            (res) => {
                failure(res);
            }
        );
    }

    version() {
    }

    setCredentials(username, password) {
        this.dao.setUsername(username);
        this.dao.setPassword(password);
    }

    isLoginValid(success, failure) {
        this.dao.authenticatedTransaction(
            '/is-login-valid',
            {},
            (resp) => {
                success(JSON.parse(resp.text).user);
            },
            (res) => {
                failure(res);
            }
        );
    }
}

class BasicNetworkDAO {
    constructor(
        url_auth,
        url_service
    ) {
        this.url_auth = url_auth;
        this.url_service = url_service;
    }

    clone(url_service) {
        var ret = new BasicNetworkDAO(this.url_auth, url_service);
        ret.setUsername(this.username);
        ret.hashed_password = this.hashed_password;

        return ret;
    }

    setUsername(username) {
        this.username = username;
    }

    setPassword(password) {
        this.hashed_password = sha512(password);
    }

    challenge(success, failure) {
        request
            .get(this.url_auth + '/challenge')
            .end((err, res) => {
                if (err) {
                    failure(err);
                } else {
                    success(JSON.parse(res.text).challenge);
                }
            });
    }

    // TODO: one day come back and add a salt for protection
    //       against rainbow tables also while doing that go
    //       ahead and utilize a PKF to increase the computational
    //       difficulty to something realisticly high
    authenticatedTransaction(url, msg, success, failure) {
        let payload = JSON.stringify(msg);

        this.challenge(
            (challenge) => {
                let phash = sha512(payload);
                let secret = sha512(phash + challenge + this.username + this.hashed_password);
                let _msg = {
                    auth: {
                        challenge: challenge,
                        chash: secret,
                        hash: phash,
                    },
                    payload: payload,
                };

                this.transaction(url, _msg, success, failure);
            },
            (res) => {
                failure(res);
            }
        );
    }

    transaction(url, msg, success, failure) {
        request
            .post(this.url_service + url)
            .send(JSON.stringify(msg))
            .end((err, res) => {
                if (err) {
                    failure(err);
                } else {
                    success(res);
                }
            });
    }
}