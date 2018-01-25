const MDACSLoginAppStateGenerator = (props) => {
    return {
        showLogin: true,
        user: null,
        alert: null,
        daoAuth: new AuthNetworkDAO(props.authUrl),
    };
};

const MDACSLoginAppMutators = {
  onCheckLogin: (props, state, setState, username, password) => {
    state.daoAuth.setCredentials(
        username, 
        password
    );

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

const MDACSLoginAppViews = {
  Main: (props, state, setState, mutators) => {
    const onCheckLogin = mutators.onCheckLogin;

    if (state.showLogin) {
        let alert_area = null;
        
        if (state.alert !== null) {
            alert_area = <Alert>{state.alert}</Alert>;
        }

        return <div>
            <div>
                <img src="utility?logo.png" height="128px" />
            </div>
            <MDACSLogin 
                onCheckLogin={(u, p) => onCheckLogin(props, state, setState, u, p)} 
            />
            {alert_area}
            </div>;
    } else {
      return <MDACSServiceDirectory
                  daoDatabase={state.daoAuth.getDatabaseDAO(props.dbUrl)}
                  daoAuth={state.daoAuth}
                  authUrl={props.authUrl}
                  dbUrl={props.dbUrl}
                  />;
    }
  },
};

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
        MDACSLoginAppMutators
      );
    }
}