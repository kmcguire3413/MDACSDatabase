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

class MDACSDataViewSummary extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {}
}

class MDACSDataItem extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {}
}

class MDACSDataView extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {}
}

class MDACSDeviceConfiguration extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {}
}

class MDACSConfigurationList extends React.Component {
    constructor(props) {
        super(props);
    }

    render() {}
}

const MDACSDatabaseModuleStateGenerator = () => {
    return {};
};

const MDACSDatabaseModuleMutators = {
    onWorkerMessage: (props, state, setState, msg) => {
        // A worker message can be of these types:
        //
        //      RequestPageData
        //          This message contains the items for a single page
        //          of data and have already been presorted by the web
        //          worker.
        //
        //      NoteSaved
        //          Signals that the note for the item has been saved.
        //      StateSaved
        //          Signals that the state for the item has been saved.
        //      ItemDeleted
        //          Signals that an item has been removed and should be
        //          marked as deleted. My plans currently are to gray
        //          this item out in the listing.
    }
};

const MDACSDatabaseModuleViews = {
    Main: (props, state, setState, mutators) => {
        return React.createElement(
            'div',
            null,
            'The database module is now loaded.'
        );
    }
};

/// <prop name="dao">DAO for the database service.</prop>
class MDACSDatabaseModule extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSDatabaseModuleStateGenerator(props);
    }

    componentDidMount() {
        // We have to serialize the needed information so that the web worker
        // can create its own DAO (data access object). This allows the sharing
        // of the DAO implementation with the web worker.
        this.state.worker = new Worker('MDACSDataWorker.js');

        /*
            The worker not only moves CPU intensive work off the UI thread but
            also helps to encapsulate the complexity and create a kind of abstraction
            for dealing with the data. The worker maintains state about the currently
            viewed sub-set of the data and responds with messages. We can also send
            directives to the worker telling it what data we need and when to update
            its local cache of data.
                 RequestPageData { startIndex: 0, endIndex: 100 }
                    Request a page of data by returning the items specified by the
                    inclusive minimum and exclusive maximum indexes.
                SaveNote { sid: 'the security id', note: 'the new note' }
                    Request that a note is saved. The worker will respond with the
                    success or failure of the operation.
                SaveState { sid: 'the security id', state: 'the new state' }
        */
        this.state.worker.postMessage({
            authUrl: this.props.dao.url_auth,
            dbUrl: this.props.dao.url_service,
            username: this.props.dao.username,
            hashedPassword: this.props.hashed_password
        });

        this.state.workers.onmessage = msg => MDACSDatabaseModuleMutators.onWorkerMessage(this.props.bind(this), this.state.bind(this), this.setState.bind(this), msg);
    }

    componentWillUnmount() {}

    render() {
        return MDACSDatabaseModuleViews.Main(this.props, this.state, this.setState.bind(this), MDACSDatabaseModuleMutators);
    }
}

const MDACSServiceDirectoryStateGenerator = props => {
    return {
        dbdao: props.daoAuth.getDatabaseDAO(props.dbUrl)
    };
};

const MDACSServiceDirectoryMutators = {};

const MDACSServiceDirectoryViews = {
    Main: (props, state, setState, mutators) => {
        return React.createElement(MDACSDatabaseModule, { dao: props.daoDatabase });
    }
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
        return MDACSServiceDirectoryViews.Main(this.props, this.state, this.setState.bind(this), MDACSServiceDirectoryMutators);
    }
}

/// <summary>
/// Initial model
/// </summary>
/// <pure/>
const MDACSLoginInitialState = {
    username: '',
    password: ''
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
    }
};

/// <summary>
/// View
/// </summary>
/// <pure/>
const MDACSLoginView = (props, state, setState, mutators) => {
    let onUserChange = e => mutators.onUserChange(e, props, state, setState);
    let onPassChange = e => mutators.onPassChange(e, props, state, setState);
    let onSubmit = e => mutators.onSubmit(e, props, state, setState);

    return React.createElement(
        'div',
        null,
        React.createElement(
            'form',
            { onSubmit: onSubmit },
            React.createElement(
                FormGroup,
                null,
                React.createElement(
                    ControlLabel,
                    null,
                    'Username'
                ),
                React.createElement(FormControl, {
                    id: 'login_username',
                    type: 'text',
                    value: state.username,
                    placeholder: 'Enter username',
                    onChange: onUserChange
                }),
                React.createElement(
                    ControlLabel,
                    null,
                    'Password'
                ),
                React.createElement(FormControl, {
                    id: 'login_password',
                    type: 'text',
                    value: state.password,
                    placeholder: 'Enter password',
                    onChange: onPassChange
                }),
                React.createElement(FormControl.Feedback, null),
                React.createElement(
                    Button,
                    { id: 'login_submit', type: 'submit' },
                    'Login'
                )
            )
        )
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
        return MDACSLoginView(this.props, this.state, this.setState.bind(this), MDACSLoginMutators);
    }
}

class DatabaseNetworkDAO {
    constructor(base_dao) {
        this.dao = base_dao;
    }

    data(success, failure) {
        this.dao.authenticatedTransaction('/data', {}, resp => {
            success(JSON.parse(resp.test));
        }, res => {
            failure(res);
        });
    }
}

class AuthNetworkDAO {
    constructor(url_auth) {
        this.dao = new BasicNetworkDAO(url_auth, url_auth);
    }

    getDatabaseDAO(url) {
        return new DatabaseNetworkDAO(this.dao.clone(url));
    }

    userSet(user, success, failure) {
        this.dao.authenticatedTransaction('/user-set', {
            user: user
        }, resp => {
            success();
        }, res => {
            failure(res);
        });
    }

    userDelete(username, success, failure) {
        this.dao.authenticatedTransaction('/user-delete', {
            username: username
        }, resp => {
            success();
        }, res => {
            failure(res);
        });
    }

    userList(success, failure) {
        this.dao.authenticatedTransaction('/user-list', {}, resp => {
            success(JSON.parse(resp.text));
        }, res => {
            failure(res);
        });
    }

    version() {}

    setCredentials(username, password) {
        this.dao.setUsername(username);
        this.dao.setPassword(password);
    }

    isLoginValid(success, failure) {
        this.dao.authenticatedTransaction('/is-login-valid', {}, resp => {
            success(JSON.parse(resp.text).user);
        }, res => {
            failure(res);
        });
    }
}

class BasicNetworkDAO {
    constructor(url_auth, url_service) {
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
        request.get(this.url_auth + '/challenge').end((err, res) => {
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

        this.challenge(challenge => {
            let phash = sha512(payload);
            let secret = sha512(phash + challenge + this.username + this.hashed_password);
            let _msg = {
                auth: {
                    challenge: challenge,
                    chash: secret,
                    hash: phash
                },
                payload: payload
            };

            this.transaction(url, _msg, success, failure);
        }, res => {
            failure(res);
        });
    }

    transaction(url, msg, success, failure) {
        request.post(this.url_service + url).send(JSON.stringify(msg)).end((err, res) => {
            if (err) {
                failure(err);
            } else {
                success(res);
            }
        });
    }
}

const MDACSLoginAppStateGenerator = props => {
    return {
        showLogin: true,
        user: null,
        alert: null,
        daoAuth: new AuthNetworkDAO(props.authUrl)
    };
};

const MDACSLoginAppMutators = {
    onCheckLogin: (props, state, setState, username, password) => {
        state.daoAuth.setCredentials(username, password);
        state.daoAuth.isLoginValid(user => {
            setState({
                showLogin: false,
                user: user,
                alert: null
            });
        }, res => {
            setState({
                alert: 'The login failed. Reason given was ' + res + '.'
            });
        });
    }
};

const MDACSLoginAppViews = {
    Main: (props, state, setState, mutators) => {
        const onCheckLogin = mutators.onCheckLogin;

        if (state.showLogin) {
            return React.createElement(
                'div',
                null,
                React.createElement(
                    'div',
                    null,
                    React.createElement('img', { src: 'utility?logo.png', height: '128px' })
                ),
                React.createElement(MDACSLogin, {
                    onCheckLogin: (u, p) => onCheckLogin(props, state, setState, u, p)
                })
            );
        } else {
            return React.createElement(MDACSServiceDirectory, {
                daoDatabase: state.daoAuth.getDatabaseDAO(props.dbUrl),
                daoAuth: state.daoAuth,
                authUrl: props.authUrl,
                dbUrl: props.dbUrl
            });
        }
    }
};

class MDACSLoginApp extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSLoginAppStateGenerator(props);
    }

    render() {
        return MDACSLoginAppViews.Main(this.props, this.state, this.setState.bind(this), MDACSLoginAppMutators);
    }
}

ReactDOM.render(React.createElement(MDACSLoginApp, {
    authUrl: "http://localhost:34002",
    dbUrl: "http://localhost:34001",
    jugglerUrl: ""
}), document.getElementById('root'));

