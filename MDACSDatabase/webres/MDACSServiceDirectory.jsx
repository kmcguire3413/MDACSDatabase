const MDACSServiceDirectoryStateGenerator = (props) => {
    return {
        dbdao: props.daoAuth.getDatabaseDAO(props.dbUrl),
    };
};
  
const MDACSServiceDirectoryMutators = {
};

const MDACSServiceDirectoryViews = {
    Main: (props, state, setState, mutators) => {
        return (<MDACSDatabaseModule dao={state.dbdao} />);
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
            MDACSServiceDirectoryMutators
        );
    }
}
