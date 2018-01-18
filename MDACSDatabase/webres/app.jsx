class DatabaseNetworkDAO {
  constructor() {
    base_dao
  } {
    this.dao = base_dao;
  }

  data(success, failure) {
    
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
      return new BasicNetworkDAO(this.url_auth, url_service);
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

/// <summary>
/// Initial model
/// </summary>
/// <pure/>
const MDACSLoginInitialState = {
    username: '',
    password: '',
};

/// <summary>
/// Controller/Mutator
/// </summary>
/// <pure/>
const MDACSLoginMutators = {
    onUserChange: (e, props, state, setState) => setState({ username: e.target.value }),
    onPassChange: (e, props, state, setState) => setState({ password: e.target.value }),
    onSubmit: (e, props, state, setState) => {
        e.preventDefault();

        if (props.onCheckLogin) {
            props.onCheckLogin(state.username, state.password);
        }
    },
};

/// <summary>
/// View
/// </summary>
/// <pure/>
const MDACSLoginView = (props, state, setState, mutators) => {
    let onUserChange = (e) => mutators.onUserChange(e, props, state, setState);
    let onPassChange = (e) => mutators.onPassChange(e, props, state, setState);
    let onSubmit = (e) => mutators.onSubmit(e, props, state, setState);

    return (
        <div>
            <form onSubmit={onSubmit}>
                <FormGroup>
                    <ControlLabel>Username</ControlLabel>
                    <FormControl
                        id="login_username"
                        type="text"
                        value={state.username}
                        placeholder="Enter username"
                        onChange={onUserChange}
                    />
                    <ControlLabel>Password</ControlLabel>
                    <FormControl
                        id="login_password"
                        type="text"
                        value={state.password}
                        placeholder="Enter password"
                        onChange={onPassChange}
                    />
                    <FormControl.Feedback />
                    <Button id="login_submit" type="submit">Login</Button>
                </FormGroup>
            </form>
        </div>
    );
};

/// <summary>
/// </summary>
/// <prop-event name="onCheckLogin(username, password)">Callback when login should be checked.</prop-event>
class MDACSLogin extends React.Component {
    constructor(props) {
        super(props);
        this.state = MDACSLoginInitialState;
    }

    render() {
        return MDACSLoginView(
            this.props,
            this.state,
            this.setState.bind(this),
            MDACSLoginMutators
        );
    }
}

const MDACSLoginAppStateGenerator = (props) => return {
  showLogin: true,
  user: null,
  alert: null,
  daoAuth: new AuthNetworkDAO(props.authUrl),
};

const MDACSLoginAppMutators = {
  onCheckLogin: (props, state, setState, username, password) => {
    state.daoAuth.setCredentials(username, password);
    state.daoAuth.isLoginValid(
        (user) => {
            setState({
                showLogin: false,
                user: user,
                alert: null,
            });
        },
        (res) => {
            setState({
                alert: 'The login failed. Reason given was ' + res + '.',
            });
        },
    );
  },
};

const MDACSLoginAppViews = () => {
  Main: (props, state, setState, mutators) => {
    const onCheckLogin = mutators.onCheckLogin;

    if (state.showLogin) {
      return '<MDACSLogin onCheckLogin={onCheckLogin} />';
    } else {
      return '<MDACSServiceDirectory
                  daoAuth={state.daoAuth}
                  authUrl={props.authUrl}
                  dbUrl={props.dbUrl}
                  />';
    }
  },
};

/*
      MDACSLoginApp
        Login
        LoggedIn
          // Controls what major panel is displayed.
          MDACSServiceDirectory
            AuthServicePanel
              *
            StorageJugglerPanel
              *
            DatabaseServicePanel
              DeviceConfigurationList
                DeviceConfiguration
              DataItemSearch
              DataItemList
                DataItem
                  DataItemDateTime
                  DataItemUser
                  DataItemDevice
                  DataItemNote
                  DataItemControls
*/

class MDACSDataViewSummary {
  constructor(props) {

  }
  render() {
  }
}

class MDACSDataItem {
  constructor(props) {
    super(props);
  }
  render() {

  }
}

class MDACSDataView {
  constructor(props) {
    super(props)
  }
  render() {
  }
}

class MDACSDeviceConfiguration extends React.Component {
  constructor(props) {
    super(props);
  }
  render() {

  }
}

class MDACSConfigurationList extends React.Component {
  constructor(props) { super(props) }
  render() {

  }
}

const MDACSDatabaseModuleStateGenerator = () => {
  return {

  };
};

const MDACSDatabaseModuleMutators = {

};

const MDACSDatabaseModuleViews = {
  Main: (props, state, setState, mutators) => {
    // Show device configurations that are avaliable.
    // Show data.
  },
};

/// <prop name="dao">DAO for the database service.</prop>
class MDACSDatabaseModule extends React.Component {
  constructor(props) {
    super(props);

    this.state = MDACSDatabaseModuleStateGenerator(props);
  }

  render() {
    return MDACSDatabaseModuleViews.Main(
      this.props,
      this.state,
      this.setState.bind(this),
      MDACSDatabaseModuleMutators
    );
  }
}

const MDACSServiceDirectoryStateGenerator = (props) => {
  return {
    dbdao: props.daoAuth.getDatabaseDAO(props.dbUrl),
  };
};

const MDACSServiceDirectoryMutators = {
};

const MDACSServiceDirectoryViews = {
  Main: (props, state, setState, mutators) => {
    return <MDACSDatabaseModule dao={state.dbdao}>;
  },
};

/// <prop name="dbUrl">The url for database service.</prop>
/// <prop name="authUrl">The url for authentication service</prop>
/// <prop name="daoAuth">DAO for authentication service</prop>
class MDACSServiceDirectory extends React.Component {
  constructor(props) {
    super(props);

    this.state = MDACSServiceDirectoryStateGenerator(props);
  }

  render() {
    return MDACSServiceDirectoryViews.Main(
      this.props,
      this.state,
      this.setState.bind(this),
      MDACSServiceSelectionMutators
    );
  }
}

class MDACSLoginApp extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSLoginAppStateGenerator(props);
    }

    render() {
      return MDACSLoginAppViews.Main(
        this.props,
        this.state,
        this.setState.bind(this),
        MDACSDatabaseAppMutators
      );
    }
}

ReactDOM.render(
    <MDACSLoginApp
      authUrl="http://localhost:34002"
      dbUrl="http://localhost:34001"
      jugglerUrl=""
    />,
    document.getElementById('root')
);
