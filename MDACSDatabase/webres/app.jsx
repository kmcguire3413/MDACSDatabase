class AuthNetworkDAO {
    constructor(
        url_auth
    ) {
        this.dao = new BasicNetworkDAO(
            url_auth,
            url_auth
        );
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

/// <prop name="dao_auth></prop>
/// <prop name="onUserAdded()"></prop>
class MDACSAuthAddUser extends React.Component {
    constructor(props) {
        super(props);

        this.onExpand = this.onExpand.bind(this);
        this.onContract = this.onContract.bind(this);
        this.onInputChange = this.onInputChange.bind(this);
        this.onAddUser = this.onAddUser.bind(this);
        this.onDismissAlert = this.onDismissAlert.bind(this);

        this.state = {
            expanded: props.expanded == true ? true : false,
            user_name: '',
            user_user: '',
            user_password: '',
            user_phone: '',
            user_email: '',
            user_admin: false,
            user_can_delete: false,
            user_userfilter: '',
            alert: null,
            success: false,
        }
    }

    onExpand() {
        this.setState({
            expanded: true,
            success: false,
            alert: null,
        });
    }

    onContract() {
        this.setState({
            expanded: false,
            success: false,
            alert: null,
        });
    }

    onInputChange(id, e) {
        let tmp = {};

        tmp['user_' + id] = e.target.value;

        this.setState(tmp)
    }

    onAddUser(e) {
        e.preventDefault();

        let user = {
            user: this.state.user_user,
            name: this.state.user_name,
            phone: this.state.user_phone,
            email: this.state.user_email,
            hash: sha512(this.state.user_password),
            admin: this.state.user_admin === 'on' ? true : false,
            can_delete: this.state.user_can_delete === 'on' ? true : false,
            userfilter: this.state.user_userfilter,
        };

        this.props.dao_auth.userSet(
            user,
            (resp) => {
                this.setState({
                    success: true,
                    alert: null,
                    expanded: false,
                });

                if (this.props.onAddedUser)
                    this.props.onAddedUser();
            },
            (res) => {
                this.setState({
                    alert: 'The add user command failed to execute on the server.',
                });
            },
        );
    }

    onDismissAlert() {
        this.setState({
            alert: null,
        });
    }

    render() {
        if (this.state.expanded) {
            let alertstuff;

            if (this.state.alert === null) {
                alertstuff = null;
            } else {
                alertstuff = <div>
                    <Alert id="adduser_alert_problem_adding" bsStyle="warning" onDismiss={this.onDismissAlert}>
                        <strong>Opps..</strong> there was a problem adding the user. The server rejected the command.
                    </Alert>
                </div>;
            }

            return (<span id="adduser_container">
                <Button id="adduser_contract_button" onClick={this.onContract}>Cancel</Button>
                <form onSubmit={this.onAddUser}>
                    <FormGroup>
                        <ControlLabel>Real Name</ControlLabel>
                        <FormControl id="adduser_realname" type="text" value={this.state.user_name} placeholder="Real name." onChange={e => this.onInputChange('name', e)} />
                        <ControlLabel>Username</ControlLabel>
                        <FormControl id="adduser_username" type="text" value={this.state.user_user} placeholder="The username used to login." onChange={e => this.onInputChange('user', e)} />
                        <ControlLabel>Password</ControlLabel>
                        <FormControl id="adduser_password" type="text" value={this.state.user_password} placeholder="Only set to new password if changing the password." onChange={e => this.onInputChange('password', e)} />
                        <ControlLabel>Contact Phone</ControlLabel>
                        <FormControl id="adduser_phone" type="text" value={this.state.user_phone ? this.state.user_phone : ''} placeholder="Phone." onChange={e => this.onInputChange('phone', e)} />
                        <ControlLabel>Contact E-Mail</ControlLabel>
                        <FormControl id="adduser_email" type="text" value={this.state.user_email ? this.state.user_email : ''} placeholder="E-Mail." onChange={e => this.onInputChange('email', e)} />
                        <ControlLabel>User Filter Expression</ControlLabel>
                        <FormControl id="adduser_userfilter" type="text" value={this.state.user_userfilter ? this.state.user_userfilter : ''} placeholder="Filter expression." onChange={e => this.onInputChange('userfilter', e)} />
                        <Checkbox id="adduser_admin" defaultChecked={this.state.user_admin} onChange={e => this.onInputChange('admin', e)}>Administrator</Checkbox>
                        <Checkbox id="adduser_can_delete" defaultChecked={this.state.user_can_delete} onChange={e => this.onInputChange('can_delete', e)}>Can Delete</Checkbox>
                        {alertstuff}
                        <Button id="adduser_submit_button" type="submit">Save New User</Button>;
                    </FormGroup>
                </form>
            </span>);
        } else {
            if (this.state.success === true) {
                return <span>
                    <Alert id="adduser_alert_success" bsStyle="success">The user was added.</Alert>
                    <Button id="adduser_expand_button" onClick={this.onExpand}>Add User</Button>
                </span>;
            } else {
                return <span><Button id="adduser_expand_button" onClick={this.onExpand}>Add User</Button></span>;
            }
        }
    }
}

/// <summary>
/// </summary>
/// <prop name="dao_auth">The authentication service data access object.</prop>
/// <prop name="onLogout()">Callback when logout is activated.</prop>
/// <prop name="user_username"></prop>
/// <prop name="user_realname"></prop>
/// <prop name="user_isadmin"></prop>
/// <prop name="user_candelete"></prop>
/// <prop name="user_userfilter"></prop>
class MDACSAuthAppBody extends React.Component {
    constructor(props) {
        super(props);

        this.onAddedUser = this.onAddedUser.bind(this);
        this.onLogout = this.onLogout.bind(this);
        this.refresh = this.refresh.bind(this);

        this.state = {
            userlist: null,
            lasterror: null,
        };

        this.refresh();
    }

    refresh() {
        this.props.dao_auth.userList(
            (resp) => {
                this.setState({
                    userlist: resp,
                    lasterror: null
                });
            },
            (res) => {
                this.setState({
                    userlist: null,
                    lasterror: res
                });
            }
        );
    }

    componentDidMount() {
    }

    componentWillUnmount() {
    }

    onAddedUser() {
        this.refresh();
    }

    onLogout(e) {
        if (this.props.onLogout) {
            this.props.onLogout();
        }
    }

    render() {
        // display login information
        // display button to refresh userlist
        // if we have userlist then render items
            // if not admin then only show our information
        let tabs;

        if (this.state.userlist == null) {
            tabs = null;
        } else {
            tabs = this.state.userlist.map(user =>
                <Tab key={user.user} eventKey={user.user} title={user.user}>
                    <MDACSUserSettings
                        dao_auth={this.props.dao_auth}
                        current_admin={this.props.user_isadmin}
                        current_user={this.props.user_username}
                        this_username={user.user}
                        user={user} />
                </Tab>
            );
        }

        let expstuff;

        if (this.props.user_userfilter == null) {
            expstuff = 'can see all items due to not having a user filter specifier';
        } else {
            expstuff = <span>
                can only see items matching the expression
                <code>{this.props.user_userfilter}</code>
            </span>;
        };

        return (
            <div>
            <Panel>
                <Panel.Heading>Active Credentials</Panel.Heading>
                <Panel.Body>
                        <ListGroup>
                            <ListGroupItem>
                                You are logged in as <code>{this.props.user_username}</code> with the real name <code>{this.props.user_realname}</code>.
                            </ListGroupItem>
                            <ListGroupItem>
                                You are <code>{this.props.user_isadmin ? 'an administrator' : 'a user with limited priviledges'}</code>.
                            </ListGroupItem>
                            <ListGroupItem>
                                You can <code>{this.props.user_candelete ? 'delete' : 'not delete'}</code> items.
                            </ListGroupItem>
                            <ListGroupItem>
                                You {expstuff}.
                            </ListGroupItem>
                        </ListGroup>
                        <Button id="logout_button" onClick={this.onLogout}>Logout</Button>
                </Panel.Body>
            </Panel>
            <Panel>
                <Panel.Heading>Existing Users</Panel.Heading>
                <Panel.Body>
                        <Tabs defaultActiveKey={1} id="user_settings_tabs">
                            {tabs != null ? tabs : ''}
                        </Tabs>
                </Panel.Body>
            </Panel>
            <Panel>
                <Panel.Heading>Add User</Panel.Heading>
                <Panel.Body>
                        <MDACSAuthAddUser
                            onAddedUser={this.onAddedUser}
                            dao_auth={this.props.dao_auth} />
                </Panel.Body>
            </Panel>
            </div>
        );

    }
}

/// <css-class>MDACSAuthAppContainer</css-class>
class MDACSAuthApp extends React.Component {
    constructor(props) {
        super(props);

        this.onCheckLogin = this.onCheckLogin.bind(this);
        this.onLogout = this.onLogout.bind(this);

        this.state = {
            need_login_shown: true,
            dao_auth: new AuthNetworkDAO('.'),
            alert: null,
        };
    }

    componentDidMount() {
    }

    componentWillUnmount() {
    }

    onLogout() {
        // Clear the credentials.
        this.state.dao_auth.setCredentials('', '');
        this.setState({ need_login_shown: true });
    }

    onCheckLogin(username, password) {
        this.state.dao_auth.setCredentials(username, password);
        this.state.dao_auth.isLoginValid(
            (user) => {
                this.setState({
                    need_login_shown: false,
                    user: user,
                    alert: null,
                });
            },
            (res) => {
                this.setState({
                    alert: 'The login failed. Reason given was ' + res + '.',
                });
            },
        );
    }

    render() {
        if (this.state.need_login_shown) {
            if (this.state.alert !== null) {
                return <div className="MDACSAuthAppContainer">
                    <Alert id="login_alert_problem" bsStyle="warning">
                        {this.state.alert}
                    </Alert>
                    <MDACSLogin onCheckLogin={this.onCheckLogin} />
                </div>;
            } else {
                return <div className="MDACSAuthAppContainer">
                    <MDACSLogin onCheckLogin={this.onCheckLogin} />
                </div>;
            }
        } else {
            return <div className="MDACSAuthAppContainer">
                <MDACSAuthAppBody
                    onLogout={this.onLogout}
                    dao_auth={this.state.dao_auth}
                    user_username={this.state.user.user}
                    user_realname={this.state.user.name}
                    user_isadmin={this.state.user.admin}
                    user_candelete={this.state.user.can_delete}
                    user_userfilter={this.state.user.userfilter}
                />
            </div>;
        }
    }
}

ReactDOM.render(
    <MDACSAuthApp />,
    document.getElementById('root')
);
